// <copyright file="CombatControllerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Runtime.InteropServices;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Offline;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests which controller (manual, MU Helper or the server's offline play) is recorded for a hit, and that after an
/// offline handover and a manual relog only the current controller can apply effects.
/// </summary>
[TestFixture]
public class CombatControllerTests
{
    private const string LoginName = "test";
    private const string CharacterName = "testChar";
    private static readonly HitInfo Hundred = new(100, 0, DamageAttributes.Undefined);

    private IGameContext _gameContext = null!;
    private Recorder _recorder = null!;

    /// <summary>Sets up a game context with a recording consumer.</summary>
    [SetUp]
    public void SetUp()
    {
        this._gameContext = GameContextTestHelper.CreateGameContext();
        this._recorder = new Recorder();
        this._gameContext.PlugInManager.RegisterPlugInAtPlugInPoint<ICombatEventPlugIn>(this._recorder);
        CombatEventHealth.Reset();
    }

    /// <summary>A player who acts manually is recorded as manual; with a running MU Helper as helper.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ManualAndHelperAreToldApartAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        var monster = await this.CreateMonsterAsync(1000).ConfigureAwait(false);

        await monster.ApplyResolvedHitAsync(player, Hundred, NewKey()).ConfigureAwait(false);
        player.Attributes!.AddElement(new ConstantElement(1), Stats.IsMuHelperActive);
        await monster.ApplyResolvedHitAsync(player, Hundred, NewKey()).ConfigureAwait(false);

        var controllers = this._recorder.Of(CombatEventKind.HitApplied).Select(e => e.Controller).ToList();
        Assert.That(controllers, Is.EqualTo(new[] { CombatController.Manual, CombatController.Helper }));
        Assert.That(monster.Health, Is.EqualTo(800));
    }

    /// <summary>
    /// A successful handover makes the server the controller; after a manual relog the old session is retired and its
    /// late actions change nothing, while the new manual session works.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task HandoverTransfersControlOnceAndRelogRetiresOldControllerAsync()
    {
        var manager = new OfflinePlayerManager();
        var realPlayer = await this.CreatePlayerWithPersistedAccountAsync().ConfigureAwait(false);
        realPlayer.TryAddMoney(1_000_000);
        realPlayer.Attributes![Stats.Level] = 100;
        var monster = await this.CreateMonsterAsync(1000).ConfigureAwait(false);

        Assert.That(await manager.StartAsync(realPlayer, LoginName).ConfigureAwait(false), Is.True);
        Assert.That(await manager.StartAsync(realPlayer, LoginName).ConfigureAwait(false), Is.False, "a second handover is refused");
        manager.TryGetPlayer(LoginName, out var offline);
        Assert.That(offline, Is.Not.Null);

        // Offline play keeps working with no game client: the server is the controller.
        Assert.That(await monster.ApplyResolvedHitAsync(offline!, Hundred, NewKey()).ConfigureAwait(false), Is.True);
        Assert.That(monster.Health, Is.EqualTo(900));
        Assert.That(this._recorder.Of(CombatEventKind.HitApplied).Last().Controller, Is.EqualTo(CombatController.OfflineServerOwned));

        // The player logs in manually again: the offline session ends.
        await manager.StopAsync(LoginName).ConfigureAwait(false);
        Assert.That(offline!.IsRetired, Is.True);

        // A delayed action of the old session has no effect.
        Assert.That(await monster.ApplyResolvedHitAsync(offline, Hundred, NewKey()).ConfigureAwait(false), Is.False);
        Assert.That(monster.Health, Is.EqualTo(900));
        Assert.That(this._recorder.Of(CombatEventKind.HitRejectedStaleController), Has.Count.EqualTo(1));

        // The new manual session is the only controller left.
        var manual = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        Assert.That(await monster.ApplyResolvedHitAsync(manual, Hundred, NewKey()).ConfigureAwait(false), Is.True);
        Assert.That(monster.Health, Is.EqualTo(800));
        Assert.That(this._recorder.Of(CombatEventKind.HitApplied).Last().Controller, Is.EqualTo(CombatController.Manual));
    }

    private static AttackKey NewKey() => new(CombatIds.NextActionId(), 0);

    private async Task<Monster> CreateMonsterAsync(int health)
    {
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.Monster, RespawnDelay = TimeSpan.FromHours(1) };
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.MaximumHealth, Value = health });
        var map = await this._gameContext.GetMapAsync(0).ConfigureAwait(false);
        var spawnArea = new MonsterSpawnArea { MonsterDefinition = definition, GameMap = map!.Definition, X1 = 100, Y1 = 100, X2 = 100, Y2 = 100, Quantity = 1 };
        var monster = new Monster(spawnArea, definition, map, NullDropGenerator.Instance, new Mock<INpcIntelligence>().Object, this._gameContext.PlugInManager, this._gameContext.PathFinderPool);
        monster.Initialize();
        return monster;
    }

    private async ValueTask<Player> CreatePlayerWithPersistedAccountAsync()
    {
        var config = this._gameContext.Configuration;
        using (var ctx = this._gameContext.PersistenceContextProvider.CreateNewPlayerContext(config))
        {
            var account = ctx.CreateNew<MUnique.OpenMU.DataModel.Entities.Account>();
            account.LoginName = LoginName;
            var character = ctx.CreateNew<MUnique.OpenMU.DataModel.Entities.Character>();
            character.Name = CharacterName;
            if (config.CharacterClasses.FirstOrDefault() is { } existingClass)
            {
                character.CharacterClass = existingClass;
            }
            else
            {
                var characterClass = ctx.CreateNew<CharacterClass>();
                characterClass.HomeMap = config.Maps.FirstOrDefault();
                character.CharacterClass = characterClass;
            }

            account.Characters.Add(character);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
        }

        var player = await PlayerTestHelper.CreatePlayerAsync(this._gameContext).ConfigureAwait(false);
        player.Account!.LoginName = LoginName;
        var mockCharacter = player.SelectedCharacter!;
        mockCharacter.Name = CharacterName;
        mockCharacter.CharacterClass ??= player.Account.UnlockedCharacterClasses.FirstOrDefault();
        if (mockCharacter.CharacterClass is null && config.CharacterClasses.FirstOrDefault() is { } cc)
        {
            mockCharacter.CharacterClass = cc;
        }

        return player;
    }

    [Guid("9A4E6B13-7C25-4D80-B3F1-5E2D8A0C6F47")]
    private sealed class Recorder : ICombatEventPlugIn
    {
        private readonly List<CombatEvent> _events = new();

        public List<CombatEvent> Of(CombatEventKind kind)
        {
            lock (this._events)
            {
                return this._events.Where(e => e.Kind == kind).ToList();
            }
        }

        public void OnCombatEvent(CombatEvent combatEvent)
        {
            lock (this._events)
            {
                this._events.Add(combatEvent);
            }
        }
    }
}
