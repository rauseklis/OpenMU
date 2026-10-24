// <copyright file="ResetCostTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Tests the costs of the resets of Raymond the Reset Master: Zen only for the first five resets, then a flat amount of Zen and
/// fruits which grow by 100 with every reset.
/// </summary>
[TestFixture]
public class ResetCostTests
{
    private const int Million = 1_000_000;

    private static ResetConfiguration CreateConfiguration()
    {
        return new ResetConfiguration
        {
            RequiredLevel = 400,
            ResetLimit = 50,
            PayItemCostsWithJewelBankFruits = true,
            MoneyCostTiers =
            [
                new ResetConfiguration.ResetMoneyCostTier { MinimumResetCount = 1, RequiredMoney = 25 * Million, MultiplyByResetCount = true },
                new ResetConfiguration.ResetMoneyCostTier { MinimumResetCount = 6, RequiredMoney = 50 * Million, MultiplyByResetCount = false },
            ],
            ItemCostTiers =
            [
                new ResetConfiguration.ResetItemCostTier { MinimumResetCount = 6, RequiredItemAmount = 100, AdditionalItemAmountPerReset = 100 },
            ],
        };
    }

    /// <summary>
    /// Tests that resets 1 to 5 cost 25 million Zen times the number of the reset and no fruits.
    /// </summary>
    [Test]
    public void FirstFiveResetsCostZenOnly()
    {
        var configuration = CreateConfiguration();
        for (var number = 1; number <= 5; number++)
        {
            var progression = ResetProgressionCalculator.Calculate(number - 1, 0, configuration);
            Assert.That(progression.NextResetCount, Is.EqualTo(number));
            Assert.That(progression.RequiredZen, Is.EqualTo(25 * Million * number), $"reset {number}");
            Assert.That(progression.RequiredItemAmount, Is.EqualTo(0), $"reset {number}");
        }
    }

    /// <summary>
    /// Tests that reset 6 costs a flat 50 million Zen and 100 fruits.
    /// </summary>
    [Test]
    public void SixthResetCostsFiftyMillionAndOneHundredFruits()
    {
        var progression = ResetProgressionCalculator.Calculate(5, 0, CreateConfiguration());
        Assert.That(progression.RequiredZen, Is.EqualTo(50 * Million));
        Assert.That(progression.RequiredItemAmount, Is.EqualTo(100));
    }

    /// <summary>
    /// Tests that resets 7 to 50 cost a flat 50 million Zen and 100 fruits more with every reset: 200 on reset 7 up to 4,500 on reset 50.
    /// </summary>
    [Test]
    public void LaterResetsAddOneHundredFruitsEach()
    {
        var configuration = CreateConfiguration();
        for (var number = 7; number <= 50; number++)
        {
            var progression = ResetProgressionCalculator.Calculate(number - 1, 0, configuration);
            Assert.That(progression.RequiredZen, Is.EqualTo(50 * Million), $"reset {number}");
            Assert.That(progression.RequiredItemAmount, Is.EqualTo(100 * (number - 5)), $"reset {number}");
        }

        Assert.That(ResetProgressionCalculator.Calculate(6, 0, configuration).RequiredItemAmount, Is.EqualTo(200));
        Assert.That(ResetProgressionCalculator.Calculate(49, 0, configuration).RequiredItemAmount, Is.EqualTo(4500));
    }

    /// <summary>
    /// Tests that without tiers the old single amount of money still applies.
    /// </summary>
    [Test]
    public void WithoutTiersTheOldMoneySettingApplies()
    {
        var configuration = new ResetConfiguration { RequiredMoney = 3, MultiplyRequiredMoneyByResetCount = true };
        Assert.That(ResetProgressionCalculator.Calculate(3, 0, configuration).RequiredZen, Is.EqualTo(12));
    }

    /// <summary>
    /// Tests that fruits are taken from the type of which the account holds most, then the next.
    /// </summary>
    [Test]
    public void FruitsAreTakenFromTheLargestStackFirst()
    {
        var state = new JewelBankState(
            new Dictionary<byte, long> { [11] = 30, [12] = 80, [13] = 0, [14] = 50, [15] = 10 },
            false);

        var plan = JewelBankService.PlanFruitSpend(state, 100);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan![12], Is.EqualTo(80));
        Assert.That(plan[14], Is.EqualTo(20));
        Assert.That(plan.Values.Sum(), Is.EqualTo(100));
    }

    /// <summary>
    /// Tests that there is no plan when the account holds fewer fruits than needed, and that jewels are never taken.
    /// </summary>
    [Test]
    public void NotEnoughFruitsGivesNoPlanAndJewelsAreIgnored()
    {
        var state = new JewelBankState(
            new Dictionary<byte, long> { [1] = 9999, [11] = 30, [15] = 10 },
            false);

        Assert.That(JewelBankService.PlanFruitSpend(state, 41), Is.Null);
        var plan = JewelBankService.PlanFruitSpend(state, 40);
        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.Keys.All(code => code is >= JewelBankKinds.FirstFruitCode and <= JewelBankKinds.LastFruitCode), Is.True);
    }

    /// <summary>
    /// Tests the requirement flags which the window shows.
    /// </summary>
    [Test]
    public void StatusFlagsTellWhichRequirementsAreMet()
    {
        var enough = new ResetNpcStatus(ResetNpcOutcome.Info, 7, 50, 400, 400, 50 * Million, 60 * Million, 200, 250);
        Assert.That(enough.CanReset, Is.True);

        var notEnoughFruits = enough with { BankFruits = 199 };
        Assert.That(notEnoughFruits.CanReset, Is.False);
        Assert.That(notEnoughFruits.Flags, Is.EqualTo((byte)(ResetNpcStatus.LevelEnough | ResetNpcStatus.ZenEnough)));

        var limit = enough with { NextResetNumber = 51 };
        Assert.That(limit.Flags & ResetNpcStatus.LimitReachedFlag, Is.Not.EqualTo(0));
        Assert.That(limit.CanReset, Is.False);
    }
}
