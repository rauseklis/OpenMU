// <copyright file="AuctionMutationStateTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the gameplay-state gate which protects every Auction House mutation.
/// </summary>
[TestFixture]
public class AuctionMutationStateTests
{
    private static readonly AuctionMutationState Eligible = new(
        HasEnteredWorld: true,
        IsAlive: true,
        IsInSafeZone: true,
        IsTeleporting: false,
        IsTrading: false,
        IsVending: false,
        HasOpenedNpc: false,
        IsDueling: false,
        IsInMiniGame: false,
        IsInGuildWar: false);

    /// <summary>
    /// Verifies that the ordinary alive, safe-zone state is accepted.
    /// </summary>
    [Test]
    public void AlivePlayerInSafeZoneWithoutConflictingActivityIsAllowed()
    {
        Assert.That(AuctionMutationEligibility.IsAllowed(Eligible), Is.True);
    }

    /// <summary>
    /// Verifies each forbidden gameplay condition independently.
    /// </summary>
    /// <param name="state">The restricted state.</param>
    [TestCaseSource(nameof(RestrictedStates))]
    public void RestrictedGameplayStateIsRejected(AuctionMutationState state)
    {
        Assert.That(AuctionMutationEligibility.IsAllowed(state), Is.False);
    }

    private static IEnumerable<AuctionMutationState> RestrictedStates()
    {
        yield return Eligible with { HasEnteredWorld = false };
        yield return Eligible with { IsAlive = false };
        yield return Eligible with { IsInSafeZone = false };
        yield return Eligible with { IsTeleporting = true };
        yield return Eligible with { IsTrading = true };
        yield return Eligible with { IsVending = true };
        yield return Eligible with { HasOpenedNpc = true };
        yield return Eligible with { IsDueling = true };
        yield return Eligible with { IsInMiniGame = true };
        yield return Eligible with { IsInGuildWar = true };
    }
}
