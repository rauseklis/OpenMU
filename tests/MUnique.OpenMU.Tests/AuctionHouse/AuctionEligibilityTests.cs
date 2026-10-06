// <copyright file="AuctionEligibilityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the mutation requirements of spec 4.10: restriction, then level, then account age.
/// </summary>
[TestFixture]
public class AuctionEligibilityTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Verifies a requester who meets every requirement is eligible.
    /// </summary>
    [Test]
    public void EligibleRequesterIsAllowed()
    {
        Assert.That(AuctionEligibility.Evaluate(6, Now.AddDays(-2), Now, 6, 24, restricted: false), Is.Null);
    }

    /// <summary>
    /// Verifies a character below the minimum level is refused, even when the account is old enough.
    /// </summary>
    [Test]
    public void LowLevelIsRefused()
    {
        Assert.That(AuctionEligibility.Evaluate(5, Now.AddDays(-2), Now, 6, 24, restricted: false), Is.EqualTo(AuctionResultCode.LevelTooLow));
    }

    /// <summary>
    /// Verifies an account younger than the minimum age is refused.
    /// </summary>
    [Test]
    public void NewAccountIsRefused()
    {
        Assert.That(AuctionEligibility.Evaluate(50, Now.AddHours(-1), Now, 6, 24, restricted: false), Is.EqualTo(AuctionResultCode.AccountTooNew));
    }

    /// <summary>
    /// Verifies a restricted account is refused before the level and age are considered.
    /// </summary>
    [Test]
    public void RestrictedAccountIsRefusedFirst()
    {
        Assert.That(AuctionEligibility.Evaluate(1, Now.AddHours(-1), Now, 6, 24, restricted: true), Is.EqualTo(AuctionResultCode.AccountRestricted));
    }
}
