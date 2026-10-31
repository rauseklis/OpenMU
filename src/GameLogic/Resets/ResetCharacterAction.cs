// <copyright file="ResetCharacterAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.GuildWar;
using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Action to reset a character at the reset NPC.
/// </summary>
public class ResetCharacterAction
{
    /// <summary>
    /// The most tiles the player may be away from the reset NPC to reset.
    /// </summary>
    public const int MaximumNpcDistance = 8;

    private readonly Player _player;
    private readonly NonPlayerCharacter _npc;
    private readonly Func<ValueTask>? _onResetDone;
    private readonly LogoutAction _logoutAction = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetCharacterAction"/> class.
    /// </summary>
    /// <param name="player">Player to reset.</param>
    /// <param name="npc">NPC which the player talks to to initiate the reset action.</param>
    /// <param name="onResetDone">Called when the costs are paid and the reset is done, before the player is moved home or logged out.</param>
    public ResetCharacterAction(Player player, NonPlayerCharacter npc, Func<ValueTask>? onResetDone = null)
    {
        this._player = player;
        this._npc = npc;
        this._onResetDone = onResetDone;
    }

    /// <summary>
    /// Determines whether the player may reset right now: alive, in a safe zone, next to the reset NPC and not busy with a trade,
    /// a personal store, a duel, a mini game, a guild war or a teleport. Like this a reset can never be used to leave a fight.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="npc">The reset NPC.</param>
    /// <returns><c>true</c> when the reset may start.</returns>
    public static bool IsStateAllowed(Player player, NonPlayerCharacter npc)
    {
        var currentState = player.PlayerState.CurrentState;
        var state = new AuctionMutationState(
            HasEnteredWorld: currentState == PlayerState.EnteredWorld || currentState == PlayerState.NpcDialogOpened,
            IsAlive: player.IsAlive,
            IsInSafeZone: player.IsAtSafezone(),
            IsTeleporting: player.IsTeleporting,
            IsTrading: player.TradingPartner is not null,
            IsVending: player.ShopStorage?.StoreOpen ?? false,
            HasOpenedNpc: false,
            IsDueling: player.DuelRoom is not null,
            IsInMiniGame: player.CurrentMiniGame is not null,
            IsInGuildWar: player.GuildWarContext?.State is GuildWarState.Requested or GuildWarState.Started);
        return AuctionMutationEligibility.IsAllowed(state)
               && player.CurrentMap == npc.CurrentMap
               && player.IsInRange(npc, MaximumNpcDistance);
    }

    /// <summary>
    /// Reads the requirements of the next reset and the current values of the player, to show them in the window of the
    /// reset NPC.
    /// </summary>
    /// <returns>The status, or <c>null</c> when resets are not available (not enabled or not configured, no character).</returns>
    public async ValueTask<ResetNpcStatus?> GetStatusAsync()
    {
        var configuration = this._player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>()?.Configuration;
        if (configuration is null || this._player.Attributes is null || this._player.SelectedCharacter is null)
        {
            return null;
        }

        var progression = ResetProgressionCalculator.Calculate(this.GetResetCount(), (int)this._player.Attributes[Stats.PointsPerReset], configuration);
        long availableItems = 0;
        if (progression.RequiredItemAmount > 0)
        {
            // The window shows what the player can pay with: the fruits in the Jewel Bank, or the items in the inventory.
            availableItems = configuration.PayItemCostsWithJewelBankFruits
                ? await this.GetBankFruitsAsync().ConfigureAwait(false) ?? 0
                : this.FindRequiredItems(configuration).Count;
        }

        return new ResetNpcStatus(
            ResetNpcOutcome.Info,
            progression.NextResetCount,
            configuration.ResetLimit ?? 0,
            configuration.RequiredLevel,
            this._player.Level,
            this.GetRequiredZen(progression),
            this._player.Money,
            progression.RequiredItemAmount,
            availableItems,
            this.CountWornItems(),
            configuration.RequireNoEquipment);
    }

    /// <summary>
    /// Reset specific character.
    /// </summary>
    /// <returns>What came of the request.</returns>
    public async ValueTask<ResetNpcOutcome> ResetCharacterAsync()
    {
        var resetFeature = this._player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>();
        if (resetFeature is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotEnabled)).ConfigureAwait(false);
            return ResetNpcOutcome.Refused;
        }

        if (this._player.Attributes is null || this._player.SelectedCharacter is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnteredTheGame)).ConfigureAwait(false);
            return ResetNpcOutcome.Refused;
        }

        if (!IsStateAllowed(this._player, this._npc))
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotInSafezone)).ConfigureAwait(false);
            return ResetNpcOutcome.Refused;
        }

        var configuration = resetFeature.Configuration;
        if (configuration is null)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotConfigured)).ConfigureAwait(false);
            return ResetNpcOutcome.Refused;
        }

        var resetProgression = ResetProgressionCalculator.Calculate(this.GetResetCount(), (int)this._player.Attributes[Stats.PointsPerReset], configuration);

        if (this._player.Level < configuration.RequiredLevel)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.RequiredLevelForReset), configuration.RequiredLevel).ConfigureAwait(false);
            return ResetNpcOutcome.RequirementsNotMet;
        }

        if (configuration.ResetLimit > 0 && resetProgression.NextResetCount > configuration.ResetLimit)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.MaximumResetsReached), configuration.ResetLimit).ConfigureAwait(false);
            return ResetNpcOutcome.LimitReached;
        }

        if (configuration.RequireNoEquipment && this.CountWornItems() > 0)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.ResetEquipmentWorn)).ConfigureAwait(false);
            return ResetNpcOutcome.RequirementsNotMet;
        }

        if (await this.TryConsumeResetCostsAsync(configuration, resetProgression).ConfigureAwait(false) is { } failure)
        {
            return failure;
        }

        this._player.Attributes[Stats.Resets] = resetProgression.NextResetCount;
        this._player.Attributes[Stats.Level] = configuration.LevelAfterReset;
        this._player.SelectedCharacter.Experience = 0;
        this.UpdateStats(configuration, resetProgression);
        await this.SaveResetAsync().ConfigureAwait(false);
        if (this._onResetDone is not null)
        {
            await this._onResetDone().ConfigureAwait(false);
        }

        if (configuration.MoveHome)
        {
            await this.MoveHomeAsync(configuration.LogOut).ConfigureAwait(false);
        }

        if (configuration.LogOut)
        {
            await this._logoutAction.LogoutAsync(this._player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
        }
        else
        {
            await this.UpdateClientStatsAsync(configuration).ConfigureAwait(false);
        }

        return ResetNpcOutcome.Done;
    }

    /// <summary>
    /// Saves the player right after the costs were paid and the reset was applied. The Zen, the destroyed items and the new reset state
    /// are changes of the same persistence context and so are written together; the Jewel Bank fruits were already committed by their own
    /// transaction. Without this save, a crash before the next periodic save would keep the payment but lose the reset.
    /// </summary>
    private async ValueTask SaveResetAsync()
    {
        try
        {
            await this._player.SaveProgressAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The reset is done in memory and will be saved with the next save; it must not be undone or reported as failed.
            this._player.Logger.LogError(ex, "The reset of {Player} could not be saved right away.", this._player);
        }
    }

    private async ValueTask ShowMessageAsync(string messageKey, params object?[] args)
    {
        var message = this._player.GetLocalizedMessage(messageKey, args);
        await this._player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(message, this._npc)).ConfigureAwait(false);
    }

    private int CountWornItems() => this._player.Inventory?.EquippedItems.Count() ?? 0;

    private int GetResetCount()
    {
        return (int)this._player.Attributes![Stats.Resets];
    }

    /// <summary>
    /// Gets the Zen of the next reset. It never exceeds the most Zen a player can carry, otherwise the reset could never be paid.
    /// </summary>
    private int GetRequiredZen(ResetProgression progression)
    {
        var maximum = this._player.GameContext.Configuration.MaximumInventoryMoney;
        return maximum > 0 ? Math.Min(progression.RequiredZen, maximum) : progression.RequiredZen;
    }

    /// <summary>
    /// Counts the fruits in the Jewel Bank of the player.
    /// </summary>
    /// <returns>The number of fruits, or <c>null</c> when the Jewel Bank is not available or could not be read.</returns>
    private async ValueTask<long?> GetBankFruitsAsync()
    {
        if (JewelBankService.Create(this._player) is not { } bank || JewelBankService.GetIdentity(this._player) is not { } who)
        {
            return null;
        }

        try
        {
            return await bank.GetFruitCountAsync(who.AccountId).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this._player.Logger.LogError(ex, "The fruits in the Jewel Bank of {Player} could not be read for a reset.", this._player);
            return null;
        }
    }

    /// <summary>
    /// Takes the costs of the reset. The fruits come first because taking them can fail; the Zen is taken only afterwards, and the
    /// fruits are given back if that does not work. Like this a problem with the database can never cost the player Zen for nothing.
    /// </summary>
    /// <returns><c>null</c> when everything is paid; otherwise what to answer instead.</returns>
    private async ValueTask<ResetNpcOutcome?> TryConsumeResetCostsAsync(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        var requiredZen = this.GetRequiredZen(resetProgression);
        if (this._player.Money < requiredZen)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), requiredZen).ConfigureAwait(false);
            return ResetNpcOutcome.RequirementsNotMet;
        }

        var fruitsFromBank = configuration.PayItemCostsWithJewelBankFruits && resetProgression.RequiredItemAmount > 0;
        JewelBankService? bank = null;
        (Guid AccountId, Guid CharacterId)? identity = null;
        IList<Item> requiredItems = [];
        if (fruitsFromBank)
        {
            bank = JewelBankService.Create(this._player);
            identity = JewelBankService.GetIdentity(this._player);
            if (bank is null || identity is null || await this.GetBankFruitsAsync().ConfigureAwait(false) is not { } bankFruits)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.ResetTemporarilyUnavailable)).ConfigureAwait(false);
                return ResetNpcOutcome.Refused;
            }

            if (bankFruits < resetProgression.RequiredItemAmount)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughItemsForReset), resetProgression.RequiredItemAmount, "fruits in the Jewel Bank").ConfigureAwait(false);
                return ResetNpcOutcome.RequirementsNotMet;
            }
        }
        else if (resetProgression.RequiredItemAmount > 0 && configuration.RequiredResetItem is { } requiredDefinition)
        {
            requiredItems = this.FindRequiredItems(configuration).Take(resetProgression.RequiredItemAmount).ToList();
            if (requiredItems.Count < resetProgression.RequiredItemAmount)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughItemsForReset), resetProgression.RequiredItemAmount, requiredDefinition.Name).ConfigureAwait(false);
                return ResetNpcOutcome.RequirementsNotMet;
            }
        }

        IReadOnlyDictionary<byte, long>? spentFruits = null;
        if (bank is not null)
        {
            JewelBankOperationResult spent;
            try
            {
                spent = await bank.SpendFruitsAsync(this._player, Guid.NewGuid(), resetProgression.RequiredItemAmount).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Nothing of the Zen was taken yet, so the player loses nothing here.
                this._player.Logger.LogError(ex, "The fruits for the reset of {Player} could not be taken from the Jewel Bank.", this._player);
                await this.ShowMessageAsync(nameof(PlayerMessage.ResetTemporarilyUnavailable)).ConfigureAwait(false);
                return ResetNpcOutcome.Refused;
            }

            if (!spent.IsSuccess)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughItemsForReset), resetProgression.RequiredItemAmount, "fruits in the Jewel Bank").ConfigureAwait(false);
                return ResetNpcOutcome.RequirementsNotMet;
            }

            spentFruits = spent.Moved;
        }

        if (requiredZen > 0 && !this._player.TryRemoveMoney(requiredZen))
        {
            await this.GiveFruitsBackAsync(bank, identity, spentFruits).ConfigureAwait(false);
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), requiredZen).ConfigureAwait(false);
            return ResetNpcOutcome.RequirementsNotMet;
        }

        foreach (var item in requiredItems)
        {
            await this._player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }

        return null;
    }

    private async ValueTask GiveFruitsBackAsync(JewelBankService? bank, (Guid AccountId, Guid CharacterId)? identity, IReadOnlyDictionary<byte, long>? spentFruits)
    {
        if (bank is null || identity is not { } who || spentFruits is null)
        {
            return;
        }

        foreach (var (kind, units) in spentFruits)
        {
            try
            {
                await bank.CreditPickupAsync(who.AccountId, who.CharacterId, kind, units).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this._player.Logger.LogError(ex, "{Units} fruits of kind {Kind} could not be given back to the Jewel Bank of {Player} after a failed reset.", units, kind, this._player);
            }
        }
    }

    /// <summary>
    /// Finds the items in the inventory (not the worn ones) which pay the item cost.
    /// </summary>
    private IList<Item> FindRequiredItems(ResetConfiguration configuration)
    {
        if (configuration.RequiredResetItem is not { } requiredDefinition || this._player.Inventory is null)
        {
            return [];
        }

        return this._player.Inventory.Items
            .Where(item => item.ItemSlot >= InventoryConstants.EquippableSlotsCount
                           && item.Definition is { } definition
                           && definition.Group == requiredDefinition.Group
                           && definition.Number == requiredDefinition.Number)
            .ToList();
    }

    private void UpdateStats(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        var investedPoints = 0;
        if (configuration.ResetStats)
        {
            foreach (var statDefinition in this._player.SelectedCharacter!.CharacterClass!.StatAttributes.Where(s => s.IncreasableByPlayer))
            {
                if (statDefinition.Attribute is not { } attribute)
                {
                    continue;
                }

                investedPoints += Math.Max(0, (int)this._player.Attributes![attribute] - (int)statDefinition.BaseValue);
                this._player.Attributes[attribute] = statDefinition.BaseValue;
            }
        }

        if (configuration.ReplacePointsPerReset)
        {
            this._player.SelectedCharacter!.LevelUpPoints = resetProgression.TotalPointsAfterReset;
        }
        else
        {
            // The stats went back to their start values, so the points which were spent on them are given back as well.
            this._player.SelectedCharacter!.LevelUpPoints += resetProgression.PointsForReset + investedPoints;
        }
    }

    private async ValueTask MoveHomeAsync(bool logOut)
    {
        var homeMapDef = this._player.SelectedCharacter!.CharacterClass!.HomeMap;
        if (homeMapDef is not { }
            || await this._player.GameContext.GetMapAsync((ushort)homeMapDef.Number).ConfigureAwait(false) is not { SafeZoneSpawnGate: { } spawnGate })
        {
            return;
        }

        if (!logOut)
        {
            // The player stays in the game, so it has to be moved for real; only changing the saved position would leave the
            // character standing where it was while its saved position says otherwise.
            await this._player.WarpToAsync(spawnGate).ConfigureAwait(false);
            return;
        }

        this._player.SelectedCharacter.PositionX = (byte)Rand.NextInt(spawnGate.X1, spawnGate.X2);
        this._player.SelectedCharacter.PositionY = (byte)Rand.NextInt(spawnGate.Y1, spawnGate.Y2);
        this._player.SelectedCharacter.CurrentMap = spawnGate.Map;
        this._player.Rotation = spawnGate.Direction;
    }

    private async ValueTask UpdateClientStatsAsync(ResetConfiguration configuration)
    {
        if (configuration.ResetStats)
        {
            await this._player.InvokeViewPlugInAsync<IUpdateCharacterBaseStatsPlugIn>(p => p.UpdateCharacterBaseStatsAsync()).ConfigureAwait(false);
        }

        await this._player.InvokeViewPlugInAsync<IUpdateLevelPlugIn>(p => p.UpdateLevelAsync()).ConfigureAwait(false);
    }
}
