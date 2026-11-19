// <copyright file="QuestMoneyRewardTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration.Quests;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Quests;

/// <summary>
/// Tests the money reward of the quest completion.
/// </summary>
[TestFixture]
public class QuestMoneyRewardTests
{
    private const short Group = 7;
    private const short Number = 1;

    /// <summary>
    /// Tests that a quest stays open, when its money reward doesn't fit into the inventory.
    /// </summary>
    [Test]
    public async ValueTask MoneyRewardAtLimitKeepsQuestOpenAsync()
    {
        var (player, questState, quest) = await CreateScenarioAsync(4500).ConfigureAwait(false);

        await new QuestCompletionAction().CompleteQuestAsync(player, Group, Number).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(4500));
        Assert.That(questState.ActiveQuest, Is.SameAs(quest));
        Assert.That(questState.LastFinishedQuest, Is.Null);
    }

    /// <summary>
    /// Tests that the money reward is paid, when it fits.
    /// </summary>
    [Test]
    public async ValueTask MoneyRewardIsPaidAsync()
    {
        var (player, questState, _) = await CreateScenarioAsync(1000).ConfigureAwait(false);

        await new QuestCompletionAction().CompleteQuestAsync(player, Group, Number).ConfigureAwait(false);

        Assert.That(player.Money, Is.EqualTo(2000));
        Assert.That(questState.ActiveQuest, Is.Null);
    }

    private static async ValueTask<(Player Player, CharacterQuestState State, QuestDefinition Quest)> CreateScenarioAsync(int startMoney)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = 5000;
        player.Money = startMoney;

        var context = player.PersistenceContext;
        var quest = context.CreateNew<QuestDefinition>();
        quest.Group = Group;
        quest.Number = Number;
        var reward = context.CreateNew<QuestReward>();
        reward.RewardType = QuestRewardType.Money;
        reward.Value = 1000;
        quest.Rewards.Add(reward);

        var questState = context.CreateNew<CharacterQuestState>();
        questState.Group = Group;
        questState.ActiveQuest = quest;
        Mock.Get(player.SelectedCharacter!).Setup(c => c.QuestStates).Returns(new List<CharacterQuestState> { questState });
        return (player, questState, quest);
    }
}
