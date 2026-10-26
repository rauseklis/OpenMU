// <copyright file="CrywolfPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Crywolf;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The plugin of the crywolf event, in which the players defend the crywolf fortress against the army of Balgass.
/// </summary>
/// <remarks>
/// The event starts at the configured times. The result of the event, the occupation state of the fortress,
/// is kept until the next event and saved in the database. It results in benefits or penalties on all game servers:
/// <list type="bullet">
///   <item>While the fortress is in peace after it has been defended, the maximum health of the monsters is reduced,
///     and the chaos machine mixes of the event tickets get an additional success rate.</item>
///   <item>While the fortress is occupied and the penalties are active, the monsters of the configured hunting maps give less experience,
///     drop jewels less often and have more maximum health.</item>
/// </list>
/// </remarks>
[PlugIn]
[Display(Name = nameof(PlugInResources.CrywolfPlugIn_Name), Description = nameof(PlugInResources.CrywolfPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("3E8B5C27-9D41-4A6F-B2C8-7F1D6E0A9B34")]
public sealed class CrywolfPlugIn : IFeaturePlugIn, IPeriodicTaskPlugIn, ISupportCustomConfiguration<CrywolfEventDefinition>, ISupportDefaultCustomConfiguration,
    IObjectAddedToMapPlugIn, IObjectRemovedFromMapPlugIn, IMonsterItemDropPlugIn, IExperienceCalculationPlugIn, IDisabledByDefault, IDisposable
{
    private readonly ConcurrentDictionary<IGameContext, CrywolfContext> _contexts = new();
    private readonly ConcurrentDictionary<IGameContext, int> _runningTicks = new();

    /// <summary>
    /// The multiplier of the maximum health of the monsters. Like the occupation state, it's the same for all game servers.
    /// </summary>
    private readonly SimpleElement _monsterHealthMultiplier = new(1.0f, AggregateType.Multiplicate);

    /// <summary>
    /// The multiplier of the maximum health of the monsters which are affected by the penalties.
    /// </summary>
    private readonly SimpleElement _monsterHealthPenaltyMultiplier = new(1.0f, AggregateType.Multiplicate);

    /// <summary>
    /// The attribute systems of the monsters, to which the health benefit is attached.
    /// </summary>
    private readonly ConditionalWeakTable<object, object> _attachedAttributeSystems = new();

    /// <summary>
    /// The attribute systems of the monsters, to which the health penalty is attached.
    /// </summary>
    private readonly ConditionalWeakTable<object, object> _attachedPenaltySystems = new();

    /// <summary>
    /// The game contexts, in which the multipliers were attached to the existing monsters since the plugin has been activated.
    /// </summary>
    private readonly ConcurrentDictionary<IGameContext, byte> _attachedGameContexts = new();

    private int _isUpdatingMultipliers;
    private int _isSubscribedToDeactivation;

    /// <summary>
    /// The benefits and penalties which apply, so that their changes are logged.
    /// </summary>
    private (bool Benefits, bool Penalties)? _appliedEffects;

    /// <inheritdoc />
    public CrywolfEventDefinition? Configuration { get; set; }

    /// <summary>
    /// Gets the context of the crywolf event of the game context, if it's running.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The context of the crywolf event.</returns>
    public static CrywolfContext? GetContext(IGameContext gameContext)
    {
        var plugIn = gameContext.FeaturePlugIns.GetPlugIn<CrywolfPlugIn>();
        return plugIn is not null && plugIn._contexts.TryGetValue(gameContext, out var context) ? context : null;
    }

    /// <summary>
    /// Gets the context of the crywolf event of the game context, which is run by this instance of the plug-in.
    /// </summary>
    /// <param name="gameContext">The game context.</param>
    /// <returns>The context of the crywolf event.</returns>
    internal CrywolfContext? GetOwnContext(IGameContext gameContext)
    {
        return this._contexts.TryGetValue(gameContext, out var context) ? context : null;
    }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (this._runningTicks.GetOrAdd(gameContext, 0) != 0
            || !this._runningTicks.TryUpdate(gameContext, 1, 0))
        {
            return;
        }

        try
        {
            var definition = this.Configuration ??= new CrywolfEventDefinition();
            if (!this._contexts.TryGetValue(gameContext, out var context))
            {
                context = new CrywolfContext(gameContext, definition);
                try
                {
                    await context.InitializeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // It's created again at the next tick, so the handlers of this one must not stay registered.
                    context.Dispose();
                    throw;
                }

                this._contexts[gameContext] = context;
            }
            else
            {
                context.UpdateDefinition(definition);
            }

            await context.TickAsync().ConfigureAwait(false);
            this.SubscribeToDeactivation(gameContext.PlugInManager);
            if (this._attachedGameContexts.TryAdd(gameContext, 0))
            {
                // The plugin was just activated, so the monsters and players which exist already didn't get the multipliers.
                await this.AttachMultipliersAsync(gameContext).ConfigureAwait(false);
            }

            await this.UpdateMultipliersAsync(gameContext).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            gameContext.LoggerFactory.CreateLogger<CrywolfPlugIn>().LogError(ex, "Unexpected error in the crywolf event.");
        }
        finally
        {
            this._runningTicks[gameContext] = 0;
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        foreach (var context in this._contexts.Values)
        {
            context.SkipWaitingTime();
        }
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return new CrywolfEventDefinition();
    }

    /// <inheritdoc />
    public ValueTask ObjectAddedToMapAsync(GameMap map, ILocateable addedObject)
    {
        if (addedObject is Monster monster)
        {
            var isBenefitAttached = this.TryAttachHealthMultiplier(monster);
            var isPenaltyAttached = this.TryAttachHealthPenaltyMultiplier(monster);
            if (isBenefitAttached || isPenaltyAttached)
            {
                // The monster got its health before it was added to the map. When it respawns, it stays on the map and keeps the multiplier.
                monster.Health = (int)monster.Attributes[Stats.MaximumHealth];
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ObjectRemovedFromMapAsync(GameMap map, ILocateable removedObject)
    {
        if (removedObject is Monster monster)
        {
            if (this._attachedAttributeSystems.Remove(monster.Attributes))
            {
                monster.Attributes.RemoveElement(this._monsterHealthMultiplier, Stats.MaximumHealth);
            }

            if (this._attachedPenaltySystems.Remove(monster.Attributes))
            {
                monster.Attributes.RemoveElement(this._monsterHealthPenaltyMultiplier, Stats.MaximumHealth);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        // It's decided here, where the killed monster is known, so that monsters of other maps keep their experience.
        // The experience already contains the rates of the channel and the other systems, so it's only multiplied.
        if (this._contexts.TryGetValue(player.GameContext, out var context))
        {
            args.Experience *= context.GetExperienceMultiplier(args.KilledObject);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void ItemDropping(AttackableNpcBase monster, Player killer, Item item, CancelEventArgs eventArgs)
    {
        if (item.Definition is { } definition
            && this._contexts.TryGetValue(killer.GameContext, out var context)
            && context.ArePenaltiesApplied
            && context.Definition.PenaltyJewels.Any(jewel => jewel.Group == definition.Group && jewel.Number == definition.Number)
            && context.Definition.IsPenaltyTarget(monster)
            && Rand.NextInt(0, 100) >= context.Definition.JewelDropPenaltyPercentage)
        {
            eventArgs.Cancel = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this._contexts.Keys.FirstOrDefault() is { } gameContext)
        {
            gameContext.PlugInManager.PlugInDeactivated -= this.OnPlugInDeactivated;
        }

        foreach (var context in this._contexts.Values)
        {
            context.Dispose();
        }

        this._contexts.Clear();
    }

    private static bool IsOrdinaryMonster(Monster monster)
    {
        // Like in the original game, it applies to all monsters, but not to the summons of players.
        // Monsters with a fixed maximum health (e.g. the gates of blood castle) keep it.
        return monster.Definition.ObjectKind == NpcObjectKind.Monster
               && monster.SummonedBy is null
               && monster.SpawnArea.MaximumHealthOverride is null;
    }

    private static void LogAppliedEffects(ILogger logger, CrywolfContext context)
    {
        var definition = context.Definition;
        if (context.AreBenefitsApplied)
        {
            logger.LogInformation(
                "The crywolf fortress is in peace. The benefits apply on all game servers: chaos mix success rate +{chaosRate} %, monster health {monsterHealth} %.",
                definition.ChaosRateBenefit,
                definition.MonsterHealthBenefitPercentage);
        }
        else if (context.ArePenaltiesApplied)
        {
            logger.LogInformation(
                "The crywolf fortress is occupied. The penalties apply on all game servers: jewel drop chance {jewelDrop} %, experience {experience} %.",
                definition.JewelDropPenaltyPercentage,
                definition.ExperiencePenaltyPercentage);
        }
        else
        {
            logger.LogInformation("Neither the benefits nor the penalties of the crywolf fortress apply. Occupation: {occupation}.", context.Occupation);
        }
    }

    private bool IsAffectedByHealthBenefit(Monster monster)
    {
        // The monsters of some maps (like the Land of Trials in the original game) keep their health.
        return IsOrdinaryMonster(monster)
               && !(monster.CurrentMap is { } map && this.Configuration?.BenefitExcludedMapNumbers.Contains((short)map.Definition.Number) == true);
    }

    private bool TryAttachHealthMultiplier(Monster monster)
    {
        if (!this.IsAffectedByHealthBenefit(monster) || this._attachedAttributeSystems.TryGetValue(monster.Attributes, out _))
        {
            return false;
        }

        this._attachedAttributeSystems.AddOrUpdate(monster.Attributes, this);
        monster.Attributes.AddElement(this._monsterHealthMultiplier, Stats.MaximumHealth);
        return true;
    }

    private bool TryAttachHealthPenaltyMultiplier(Monster monster)
    {
        if (this.Configuration?.IsPenaltyTarget(monster) != true
            || this._attachedPenaltySystems.TryGetValue(monster.Attributes, out _))
        {
            return false;
        }

        this._attachedPenaltySystems.AddOrUpdate(monster.Attributes, this);
        monster.Attributes.AddElement(this._monsterHealthPenaltyMultiplier, Stats.MaximumHealth);
        return true;
    }

    /// <summary>
    /// Attaches the multipliers to the monsters of the game context, which exist already.
    /// </summary>
    private async ValueTask AttachMultipliersAsync(IGameContext gameContext)
    {
        foreach (var map in await gameContext.GetMapsAsync().ConfigureAwait(false))
        {
            foreach (var monster in map.GetNpcsInRange(new Point(128, 128), byte.MaxValue).OfType<Monster>())
            {
                this.TryAttachHealthMultiplier(monster);
                this.TryAttachHealthPenaltyMultiplier(monster);
            }
        }
    }

    /// <summary>
    /// Subscribes to the deactivation of this plugin, so that its effects can be undone.
    /// </summary>
    private void SubscribeToDeactivation(PlugInManager plugInManager)
    {
        if (Interlocked.Exchange(ref this._isSubscribedToDeactivation, 1) == 0)
        {
            plugInManager.PlugInDeactivated += this.OnPlugInDeactivated;
        }
    }

    private void OnPlugInDeactivated(object? sender, PlugInEventArgs e)
    {
        if (e.PlugInType != this.GetType())
        {
            return;
        }

        // The multipliers stay attached, but without an effect. When the plugin is activated again, they're
        // attached to the monsters and players which came in the meantime, and get their values again.
        this._monsterHealthMultiplier.Value = 1;
        this._monsterHealthPenaltyMultiplier.Value = 1;
        this._appliedEffects = null;
        this._attachedGameContexts.Clear();

        // A running war has to end, too. Otherwise, the army, the contracts and the war terrain would stay frozen.
        _ = Task.Run(this.AbortEventsAsync);
    }

    /// <summary>
    /// Stops the events of all game contexts, without interfering with a tick which is currently running.
    /// </summary>
    private async Task AbortEventsAsync()
    {
        foreach (var (gameContext, context) in this._contexts.ToList())
        {
            try
            {
                this._runningTicks.GetOrAdd(gameContext, 0);
                while (!this._runningTicks.TryUpdate(gameContext, 1, 0))
                {
                    await Task.Delay(50).ConfigureAwait(false);
                }

                try
                {
                    await context.AbortAsync().ConfigureAwait(false);
                }
                finally
                {
                    this._runningTicks[gameContext] = 0;
                }
            }
            catch (Exception ex)
            {
                gameContext.LoggerFactory.CreateLogger<CrywolfPlugIn>().LogError(ex, "Couldn't stop the crywolf event.");
            }
        }
    }

    private async ValueTask UpdateMultipliersAsync(GameContext gameContext)
    {
        if (Interlocked.Exchange(ref this._isUpdatingMultipliers, 1) != 0)
        {
            return;
        }

        try
        {
            // The occupation state is the same on all game servers, but the one which runs the event knows its changes first.
            var context = this._contexts.Values.FirstOrDefault(c => c.IsEventServer) ?? this._contexts.Values.FirstOrDefault();
            if (context is null)
            {
                return;
            }

            var appliedEffects = (context.AreBenefitsApplied, context.ArePenaltiesApplied);
            if (this._appliedEffects != appliedEffects)
            {
                this._appliedEffects = appliedEffects;
                LogAppliedEffects(gameContext.LoggerFactory.CreateLogger<CrywolfPlugIn>(), context);
            }

            var isHealthReduced = false;
            var healthMultiplier = context.MonsterHealthMultiplier;
            if (Math.Abs(this._monsterHealthMultiplier.Value - healthMultiplier) > float.Epsilon)
            {
                isHealthReduced |= healthMultiplier < this._monsterHealthMultiplier.Value;
                this._monsterHealthMultiplier.Value = healthMultiplier;
            }

            var penaltyMultiplier = context.MonsterHealthPenaltyMultiplier;
            if (Math.Abs(this._monsterHealthPenaltyMultiplier.Value - penaltyMultiplier) > float.Epsilon)
            {
                isHealthReduced |= penaltyMultiplier < this._monsterHealthPenaltyMultiplier.Value;
                this._monsterHealthPenaltyMultiplier.Value = penaltyMultiplier;
            }

            // A raised maximum health doesn't heal the living monsters, but a lowered one limits their health.
            if (isHealthReduced)
            {
                await this.LimitMonsterHealthAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            Volatile.Write(ref this._isUpdatingMultipliers, 0);
        }
    }

    /// <summary>
    /// Limits the health of the living monsters to their reduced maximum health.
    /// </summary>
    private async ValueTask LimitMonsterHealthAsync()
    {
        foreach (var gameContext in this._contexts.Keys)
        {
            foreach (var map in await gameContext.GetMapsAsync().ConfigureAwait(false))
            {
                foreach (var monster in map.GetNpcsInRange(new Point(128, 128), byte.MaxValue).OfType<Monster>())
                {
                    var maximumHealth = (int)monster.Attributes[Stats.MaximumHealth];
                    if (monster.Health > maximumHealth)
                    {
                        monster.Health = maximumHealth;
                    }
                }
            }
        }
    }
}
