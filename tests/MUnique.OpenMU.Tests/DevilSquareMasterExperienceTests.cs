// <copyright file="DevilSquareMasterExperienceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.MiniGames;
using MUnique.OpenMU.GameLogic.Premium;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests actual completion rewards through the minigame reward service.
/// </summary>
[TestFixture]
public class DevilSquareMasterExperienceTests
{
    /// <summary>
    /// DS seven grants the full fixed amount across Master level-ups without rate multiplication.
    /// </summary>
    /// <param name="amount">The proposed rank reward.</param>
    [TestCase(60_000_000)]
    [TestCase(55_000_000)]
    [TestCase(50_000_000)]
    [TestCase(45_000_000)]
    public async Task SevenGrantsFullMasterRewardAsync(int amount)
    {
        var player = await CreatePlayerAsync(true, 400).ConfigureAwait(false);
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, amount).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.MasterExperience, Is.EqualTo(amount));
        Assert.That(player.SelectedCharacter.Experience, Is.Zero);
        Assert.That(player.Attributes![Stats.MasterLevel], Is.GreaterThan(0));
    }

    /// <summary>
    /// Other DS levels and Blood Castle never route completion rewards to Master EXP.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="level">The event level.</param>
    [TestCase(MiniGameType.DevilSquare, 1)]
    [TestCase(MiniGameType.DevilSquare, 2)]
    [TestCase(MiniGameType.DevilSquare, 3)]
    [TestCase(MiniGameType.DevilSquare, 4)]
    [TestCase(MiniGameType.DevilSquare, 5)]
    [TestCase(MiniGameType.DevilSquare, 6)]
    [TestCase(MiniGameType.BloodCastle, 7)]
    [TestCase(MiniGameType.BloodCastle, 8)]
    public async Task OtherEventsNeverGrantMasterExperienceAsync(MiniGameType type, byte level)
    {
        var player = await CreatePlayerAsync(true, 400).ConfigureAwait(false);
        await GiveRewardAsync(player, type, level, 1000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.MasterExperience, Is.Zero);
    }

    /// <summary>
    /// Characters below the normal cap still get normal experience and cannot gain Master EXP early.
    /// </summary>
    /// <param name="masterClass">Whether the character has completed the class promotion.</param>
    [TestCase(false)]
    [TestCase(true)]
    public async Task BelowCapStillGetsNormalExperienceAsync(bool masterClass)
    {
        var player = await CreatePlayerAsync(masterClass, 399).ConfigureAwait(false);
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, 1000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.Experience, Is.EqualTo(1000));
        Assert.That(player.SelectedCharacter.MasterExperience, Is.Zero);
    }

    /// <summary>
    /// EXP blocking still prevents a Master reward and cannot leave the grant loop running.
    /// </summary>
    [Test]
    public async Task BlockedExperienceDoesNotGrantRewardAsync()
    {
        var player = await CreatePlayerAsync(true, 400).ConfigureAwait(false);
        player.Attributes!.AddElement(new SimpleElement(1, AggregateType.AddRaw), PremiumBuffStats.ExperienceBlocked);
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, 80_000_000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.MasterExperience, Is.Zero);
    }

    /// <summary>
    /// The maximum Master level still prevents extra experience.
    /// </summary>
    [Test]
    public async Task MasterCapDoesNotGrantExtraRewardAsync()
    {
        var player = await CreatePlayerAsync(true, 400).ConfigureAwait(false);
        player.Attributes![Stats.MasterLevel] = player.GameContext.Configuration.MaximumMasterLevel;
        var before = player.GameContext.MasterExperienceTable[player.GameContext.Configuration.MaximumMasterLevel];
        player.SelectedCharacter!.MasterExperience = before;
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, 80_000_000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter.MasterExperience, Is.EqualTo(before));
    }

    /// <summary>
    /// Reaching the normal cap without the Master class promotion is not sufficient.
    /// </summary>
    [Test]
    public async Task UnpromotedCharacterCannotReceiveMasterExperienceAsync()
    {
        var player = await CreatePlayerAsync(false, 400).ConfigureAwait(false);
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, 1000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.MasterExperience, Is.Zero);
    }

    /// <summary>
    /// Administrators can still prevent experience overflow across a level boundary.
    /// </summary>
    [Test]
    public async Task OverflowPreventionIsRespectedAsync()
    {
        var player = await CreatePlayerAsync(true, 400).ConfigureAwait(false);
        player.GameContext.Configuration.PreventExperienceOverflow = true;
        await GiveRewardAsync(player, MiniGameType.DevilSquare, 7, 80_000_000).ConfigureAwait(false);
        Assert.That(player.SelectedCharacter!.MasterExperience, Is.EqualTo(player.GameContext.MasterExperienceTable[1]));
    }

    private static async Task GiveRewardAsync(Player player, MiniGameType type, byte level, int amount)
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var definition = context.CreateNew<MiniGameDefinition>();
        definition.Type = type;
        definition.GameLevel = level;
        var reward = context.CreateNew<MiniGameReward>();
        reward.RewardType = MiniGameRewardType.Experience;
        reward.RewardAmount = amount;
        definition.Rewards.Add(reward);
        var service = new MiniGameRewardService(definition, player.GameContext, null!, () => NullDropGenerator.Instance,
            () => TimeSpan.Zero, () => player, NullLogger<MiniGameRewardService>.Instance, definition);
        await service.GiveRewardsAndGetBonusScoreAsync(player, 1).ConfigureAwait(false);
    }

    private static async Task<Player> CreatePlayerAsync(bool masterClass, short level)
    {
        var provider = new InMemoryPersistenceContextProvider();
        using var context = provider.CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        configuration.RecoveryInterval = int.MaxValue;
        configuration.MaximumLevel = 400;
        configuration.MaximumMasterLevel = 200;
        configuration.ExperienceRate = 100;
        configuration.MasterExperienceRate = 100;
        var map = context.CreateNew<GameMapDefinition>();
        map.ExpMultiplier = 100;
        configuration.Maps.Add(map);
        var initializer = new MapInitializer(configuration, new NullLogger<MapInitializer>(), NullDropGenerator.Instance, null);
        var game = new GameContext(configuration, provider, initializer, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        initializer.PlugInManager = game.PlugInManager;
        initializer.PathFinderPool = game.PathFinderPool;
        var player = await PlayerTestHelper.CreatePlayerAsync(game).ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass!.IsMasterClass = masterClass;
        player.Attributes![Stats.Level] = level;
        player.Attributes[Stats.MasterLevel] = 0;
        player.Attributes[Stats.MasterPointsPerLevelUp] = 1;
        player.Attributes.AddElement(new SimpleElement(100, AggregateType.AddRaw), Stats.ExperienceRate);
        player.Attributes.AddElement(new SimpleElement(100, AggregateType.AddRaw), Stats.MasterExperienceRate);
        return player;
    }
}
