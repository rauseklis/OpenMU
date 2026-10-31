// <copyright file="ResetCharacterActionTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Tests for <see cref="ResetCharacterAction"/>.
/// </summary>
[TestFixture]
public class ResetCharacterActionTest
{
    /// <summary>
    /// Verifies that reset is rejected when required items are missing, without charging zen.
    /// </summary>
    [Test]
    public async Task NotEnoughItemsRejectsResetAndKeepsZenAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 1).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(0));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies that reset consumes configured zen and required items when all conditions are met.
    /// </summary>
    [Test]
    public async Task EnoughItemsAndZenConsumesCostsOnceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        Assert.That(player.Level, Is.EqualTo(400));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That(
            player.Inventory!.Items.Count(i => i.Definition is { } definition && definition.Group == requiredItem.Group && definition.Number == requiredItem.Number),
            Is.EqualTo(2));
        var progression = ResetProgressionCalculator.Calculate((int)player.Attributes[Stats.Resets], (int)player.Attributes[Stats.PointsPerReset], configuration);
        Assert.That(progression.RequiredItemAmount, Is.EqualTo(2));
        Assert.That(progression.RequiredZen, Is.EqualTo(500));
        var action = new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.Money, Is.EqualTo(500));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(0));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(800));
        Assert.That((int)player.Attributes[Stats.Level], Is.EqualTo(1));
    }

    /// <summary>
    /// Verifies cumulative point replacement across multiple resets when point tiers are configured.
    /// </summary>
    [Test]
    public async Task ReplacePointsUsesCumulativeTierTotalAcrossResetsAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 2_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 4).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(800));
        Assert.That(player.Money, Is.EqualTo(1_500));

        player.Attributes[Stats.Level] = 400;
        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(2));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(1_600));
        Assert.That(player.Money, Is.EqualTo(500));
    }

    /// <summary>
    /// Verifies add mode behavior when replace mode is disabled.
    /// </summary>
    [Test]
    public async Task TieredPointsAreAddedWhenReplaceIsDisabledAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        player.SelectedCharacter!.LevelUpPoints = 1_000;

        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.ReplacePointsPerReset = false;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);

        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var action = new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false));

        await action.ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(1));
        Assert.That(player.SelectedCharacter!.LevelUpPoints, Is.EqualTo(1_800));
    }

    /// <summary>
    /// Verifies that a reset outside a safe zone is refused and takes nothing: it must not be usable to leave a fight.
    /// </summary>
    [Test]
    public async Task ResetOutsideASafeZoneIsRefusedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = this.CreateConfiguration(requiredItem) }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var npc = await PrepareAtNpcAsync(player).ConfigureAwait(false);
        player.CurrentMap!.Terrain.SafezoneMap[player.Position.X, player.Position.Y] = false;

        var outcome = await new ResetCharacterAction(player, npc).ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(outcome, Is.EqualTo(ResetNpcOutcome.Refused));
        Assert.That((int)player.Attributes[Stats.Resets], Is.EqualTo(0));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That(player.Inventory!.Items.Count(i => i.Definition == requiredItem), Is.EqualTo(2));
    }

    /// <summary>
    /// Verifies that a reset is refused when the player is not next to the reset NPC any more.
    /// </summary>
    [Test]
    public async Task ResetFarFromTheNpcIsRefusedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = this.CreateConfiguration(requiredItem) }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);
        var npc = await PrepareAtNpcAsync(player, npcDistance: ResetCharacterAction.MaximumNpcDistance + 20).ConfigureAwait(false);

        var outcome = await new ResetCharacterAction(player, npc).ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(outcome, Is.EqualTo(ResetNpcOutcome.Refused));
        Assert.That(player.Money, Is.EqualTo(1_000));
    }

    /// <summary>
    /// Verifies that the window of the reset NPC counts the required items of the inventory when they pay the reset, so the
    /// Reset button is not blocked while the player carries enough.
    /// </summary>
    [Test]
    public async Task StatusCountsInventoryItemsWhenTheyPayTheResetAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = this.CreateConfiguration(requiredItem) }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);

        var status = await new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false)).GetStatusAsync().ConfigureAwait(false);

        Assert.That(status, Is.Not.Null);
        Assert.That(status!.Value.RequiredFruits, Is.EqualTo(2));
        Assert.That(status.Value.BankFruits, Is.EqualTo(2));
        Assert.That(status.Value.CanReset, Is.True);
    }

    /// <summary>
    /// Verifies that the Zen of a reset never exceeds what a player can carry, otherwise the reset could never be paid.
    /// </summary>
    [Test]
    public async Task RequiredZenNeverExceedsTheMostAPlayerCanCarryAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.GameContext.Configuration.MaximumInventoryMoney = 2_000_000_000;
        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.RequiredMoney = 1_000_000_000;
        configuration.MultiplyRequiredMoneyByResetCount = true;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        player.Attributes[Stats.Resets] = 9;

        var status = await new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false)).GetStatusAsync().ConfigureAwait(false);

        Assert.That(status!.Value.RequiredZen, Is.EqualTo(2_000_000_000));
    }

    /// <summary>
    /// Verifies that the stat points a player has spent are given back when a reset puts the stats back to their start values
    /// and the points of the reset are added to the existing ones.
    /// </summary>
    [Test]
    public async Task SpentStatPointsAreGivenBackWhenPointsAreAddedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        player.SelectedCharacter!.LevelUpPoints = 100;
        player.Attributes[Stats.BaseStrength] = 28 + 500;
        var requiredItem = new ItemDefinition { Name = "Jewel of Creation", Group = 14, Number = 22, Width = 1, Height = 1 };
        var configuration = this.CreateConfiguration(requiredItem);
        configuration.ResetStats = true;
        configuration.ReplacePointsPerReset = false;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        await AddRequiredItemsAsync(player, requiredItem, 2).ConfigureAwait(false);

        await new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false)).ResetCharacterAsync().ConfigureAwait(false);

        Assert.That((int)player.Attributes[Stats.BaseStrength], Is.EqualTo(28));
        Assert.That(player.SelectedCharacter.LevelUpPoints, Is.EqualTo(100 + 800 + 500));
    }

    /// <summary>
    /// Verifies that a problem with the Jewel Bank while paying the fruits leaves the Zen of the player alone.
    /// </summary>
    [Test]
    public async Task ABankErrorWhilePayingFruitsKeepsTheZenAsync()
    {
        var bank = new Mock<IJewelBankRepository>();
        bank.Setup(b => b.GetStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new JewelBankState(new Dictionary<byte, long> { [11] = 500 }, false));
        bank.Setup(b => b.SpendAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<byte, long>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is down"));
        var player = await this.CreateBankPlayerAsync(bank.Object).ConfigureAwait(false);

        var outcome = await new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false)).ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(outcome, Is.EqualTo(ResetNpcOutcome.Refused));
        Assert.That(player.Money, Is.EqualTo(1_000));
        Assert.That((int)player.Attributes![Stats.Resets], Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that a reset paid with Jewel Bank fruits takes the fruits and the Zen once and resets the character.
    /// </summary>
    [Test]
    public async Task ResetPaidWithBankFruitsTakesFruitsAndZenAsync()
    {
        var bank = new Mock<IJewelBankRepository>();
        bank.Setup(b => b.GetStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new JewelBankState(new Dictionary<byte, long> { [11] = 500 }, false));
        bank.Setup(b => b.SpendAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<byte, long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid account, Guid character, Guid operation, IReadOnlyDictionary<byte, long> units, CancellationToken token)
                => new JewelBankOperationResult(JewelBankResultCode.Success, units, null));
        var player = await this.CreateBankPlayerAsync(bank.Object).ConfigureAwait(false);

        var outcome = await new ResetCharacterAction(player, await PrepareAtNpcAsync(player).ConfigureAwait(false)).ResetCharacterAsync().ConfigureAwait(false);

        Assert.That(outcome, Is.EqualTo(ResetNpcOutcome.Done));
        Assert.That(player.Money, Is.EqualTo(500));
        Assert.That((int)player.Attributes![Stats.Resets], Is.EqualTo(1));
        bank.Verify(b => b.SpendAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.Is<IReadOnlyDictionary<byte, long>>(u => u[11] == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class BankProvider : InMemoryPersistenceContextProvider, IPersistenceContextProvider
    {
        private readonly IJewelBankRepository _bank;

        public BankProvider(IJewelBankRepository bank)
        {
            this._bank = bank;
        }

        IJewelBankRepository? IPersistenceContextProvider.CreateJewelBankRepository() => this._bank;
    }

    private static async ValueTask AddRequiredItemsAsync(Player player, ItemDefinition requiredItem, int count)
    {
        for (byte i = 0; i < count; i++)
        {
            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = requiredItem;
            item.Durability = 1;
            var added = await player.Inventory!.AddItemAsync(item).ConfigureAwait(false);
            Assert.That(added, Is.True);
        }
    }

    /// <summary>
    /// Puts the player into the world on map 0, standing in a safe zone, and creates the reset NPC next to the player.
    /// </summary>
    private static async ValueTask<NonPlayerCharacter> PrepareAtNpcAsync(Player player, int npcDistance = 1)
    {
        var map = await player.GameContext.GetMapAsync(0).ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Could not resolve map 0 for NPC test setup.");
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        await map.AddAsync(player).ConfigureAwait(false);
        map.Terrain.SafezoneMap[player.Position.X, player.Position.Y] = true;

        var x = (byte)(player.Position.X + npcDistance);
        var spawnArea = new MonsterSpawnArea { X1 = x, X2 = x, Y1 = player.Position.Y, Y2 = player.Position.Y };
        var definition = new MonsterDefinition
        {
            Number = ResetCharacterNpcPlugin.ResetNpcNumber,
            ObjectKind = NpcObjectKind.PassiveNpc,
            Designation = "Reset Helper",
        };

        return new NonPlayerCharacter(spawnArea, definition, map) { Position = new MUnique.OpenMU.Pathfinding.Point(x, player.Position.Y) };
    }

    private async ValueTask<Player> CreateBankPlayerAsync(IJewelBankRepository bank)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(new BankProvider(bank)).ConfigureAwait(false);
        player.Attributes![Stats.Level] = 400;
        player.Money = 1_000;
        var configuration = this.CreateConfiguration(null);
        configuration.PayItemCostsWithJewelBankFruits = true;
        player.GameContext.FeaturePlugIns.AddPlugIn(new ResetFeaturePlugIn { Configuration = configuration }, true);
        return player;
    }

    private ResetConfiguration CreateConfiguration(ItemDefinition? requiredItem)
    {
        return new ResetConfiguration
        {
            RequiredLevel = 400,
            LevelAfterReset = 1,
            RequiredMoney = 500,
            MultiplyRequiredMoneyByResetCount = true,
            RequiredResetItem = requiredItem,
            ItemCostTiers =
            [
                new() { MinimumResetCount = 1, RequiredItemAmount = 2 },
            ],
            PointsTiers =
            [
                new() { MinimumResetCount = 1, PointsGranted = 800 },
            ],
            MoveHome = false,
            LogOut = false,
            ResetStats = false,
            ReplacePointsPerReset = true,
        };
    }
}
