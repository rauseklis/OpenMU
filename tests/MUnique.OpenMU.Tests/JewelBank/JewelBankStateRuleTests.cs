// <copyright file="JewelBankStateRuleTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.GameLogic.JewelBank;

/// <summary>
/// Tests when the Jewel Bank may be used by hand: everywhere, but not while another activity conflicts.
/// </summary>
[TestFixture]
public class JewelBankStateRuleTests
{
    private static readonly AuctionMutationState Idle = new(
        HasEnteredWorld: true,
        IsAlive: true,
        IsInSafeZone: false,
        IsTeleporting: false,
        IsTrading: false,
        IsVending: false,
        HasOpenedNpc: false,
        IsDueling: false,
        IsInMiniGame: false,
        IsInGuildWar: false);

    /// <summary>
    /// The bank works outside safe zones, and in them.
    /// </summary>
    /// <param name="inSafeZone">Whether the player stands in a safe zone.</param>
    [TestCase(false)]
    [TestCase(true)]
    public void WorksInTheFieldAndInSafeZones(bool inSafeZone)
    {
        Assert.That(JewelBankService.IsStateAllowed(Idle with { IsInSafeZone = inSafeZone }), Is.True);
    }

    /// <summary>
    /// Every other conflicting state still blocks the bank, also outside safe zones.
    /// </summary>
    [Test]
    public void ConflictingStatesStillBlock()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JewelBankService.IsStateAllowed(Idle with { HasEnteredWorld = false }), Is.False, "not in the world");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsAlive = false }), Is.False, "dead");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsTeleporting = true }), Is.False, "teleporting");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsTrading = true }), Is.False, "trading");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsVending = true }), Is.False, "own shop open");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { HasOpenedNpc = true }), Is.False, "NPC window open");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsDueling = true }), Is.False, "duel");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsInMiniGame = true }), Is.False, "mini game");
            Assert.That(JewelBankService.IsStateAllowed(Idle with { IsInGuildWar = true }), Is.False, "guild war");
        });
    }
}
