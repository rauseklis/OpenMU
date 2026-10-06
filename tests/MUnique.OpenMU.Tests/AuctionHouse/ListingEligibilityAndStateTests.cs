// <copyright file="ListingEligibilityAndStateTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies the item eligibility rules for listing, and the listing and collection state-transition matrix.
/// </summary>
[TestFixture]
public class ListingEligibilityAndStateTests
{
    private static ListingCandidate Eligible() => new(
        IsTradable: true,
        IsPersonalStoreSellable: true,
        IsBoundToCharacter: false,
        IsQuestItem: false,
        Durability: 10,
        Location: ItemLocation.MainInventory,
        IsAlreadyReferenced: false);

    /// <summary>
    /// Verifies a clean item in the main inventory is eligible.
    /// </summary>
    [Test]
    public void CleanInventoryItemIsEligible()
    {
        Assert.That(ListingEligibility.Evaluate(Eligible()), Is.EqualTo(ListingEligibilityResult.Eligible));
    }

    /// <summary>
    /// Verifies each ineligible condition yields its own reason.
    /// </summary>
    [Test]
    public void EachIneligibleConditionHasItsOwnReason()
    {
        var eligible = Eligible();

        Assert.That(ListingEligibility.Evaluate(eligible with { IsTradable = false }), Is.EqualTo(ListingEligibilityResult.NotTradable));
        Assert.That(ListingEligibility.Evaluate(eligible with { IsPersonalStoreSellable = false }), Is.EqualTo(ListingEligibilityResult.NotSellable));
        Assert.That(ListingEligibility.Evaluate(eligible with { IsBoundToCharacter = true }), Is.EqualTo(ListingEligibilityResult.Bound));
        Assert.That(ListingEligibility.Evaluate(eligible with { IsQuestItem = true }), Is.EqualTo(ListingEligibilityResult.QuestItem));
        Assert.That(ListingEligibility.Evaluate(eligible with { Durability = 0 }), Is.EqualTo(ListingEligibilityResult.ZeroDurability));
        Assert.That(ListingEligibility.Evaluate(eligible with { IsAlreadyReferenced = true }), Is.EqualTo(ListingEligibilityResult.AlreadyReferenced));
    }

    /// <summary>
    /// Verifies that only items in the main inventory are eligible, whatever their other state.
    /// </summary>
    /// <param name="location">The item's location.</param>
    [TestCase(ItemLocation.Equipped)]
    [TestCase(ItemLocation.Trade)]
    [TestCase(ItemLocation.PersonalStore)]
    [TestCase(ItemLocation.Warehouse)]
    [TestCase(ItemLocation.ChaosMachine)]
    [TestCase(ItemLocation.TemporaryContainer)]
    public void OnlyMainInventoryItemsAreEligible(ItemLocation location)
    {
        Assert.That(ListingEligibility.Evaluate(Eligible() with { Location = location }), Is.EqualTo(ListingEligibilityResult.WrongLocation));
    }

    /// <summary>
    /// Verifies the valid listing transitions, and that every other transition is rejected.
    /// </summary>
    /// <param name="from">The current state.</param>
    /// <param name="to">The requested state.</param>
    /// <param name="allowed">Whether the transition is allowed.</param>
    [TestCase(ListingStatus.Active, ListingStatus.Sold, true)]
    [TestCase(ListingStatus.Active, ListingStatus.Expired, true)]
    [TestCase(ListingStatus.Active, ListingStatus.Cancelled, true)]
    [TestCase(ListingStatus.Active, ListingStatus.AdminRemoved, true)]
    [TestCase(ListingStatus.Active, ListingStatus.Active, false)]
    [TestCase(ListingStatus.Sold, ListingStatus.Active, false)]
    [TestCase(ListingStatus.Sold, ListingStatus.Expired, false)]
    [TestCase(ListingStatus.Expired, ListingStatus.Sold, false)]
    [TestCase(ListingStatus.Expired, ListingStatus.Cancelled, false)]
    [TestCase(ListingStatus.Cancelled, ListingStatus.Active, false)]
    [TestCase(ListingStatus.AdminRemoved, ListingStatus.Cancelled, false)]
    public void ListingTransitionsAreExactlyTheApprovedSet(ListingStatus from, ListingStatus to, bool allowed)
    {
        Assert.That(ListingStateMachine.CanTransition(from, to), Is.EqualTo(allowed));
    }

    /// <summary>
    /// Verifies a seller may cancel only an active listing with no bids.
    /// </summary>
    [Test]
    public void CancellationRequiresActiveListingWithoutBids()
    {
        Assert.That(ListingStateMachine.CanCancel(ListingStatus.Active, bidCount: 0), Is.True);
        Assert.That(ListingStateMachine.CanCancel(ListingStatus.Active, bidCount: 1), Is.False);
        Assert.That(ListingStateMachine.CanCancel(ListingStatus.Sold, bidCount: 0), Is.False);
    }

    /// <summary>
    /// Verifies the collection transitions: a pending entry may be claimed partly or in full, and a partly claimed
    /// entry may be claimed further, but a claimed entry is final.
    /// </summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The requested status.</param>
    /// <param name="allowed">Whether the transition is allowed.</param>
    [TestCase(CollectionStatus.Pending, CollectionStatus.PartiallyClaimed, true)]
    [TestCase(CollectionStatus.Pending, CollectionStatus.Claimed, true)]
    [TestCase(CollectionStatus.PartiallyClaimed, CollectionStatus.PartiallyClaimed, true)]
    [TestCase(CollectionStatus.PartiallyClaimed, CollectionStatus.Claimed, true)]
    [TestCase(CollectionStatus.Claimed, CollectionStatus.Pending, false)]
    [TestCase(CollectionStatus.Claimed, CollectionStatus.PartiallyClaimed, false)]
    [TestCase(CollectionStatus.PartiallyClaimed, CollectionStatus.Pending, false)]
    public void CollectionTransitionsAreExactlyTheApprovedSet(CollectionStatus from, CollectionStatus to, bool allowed)
    {
        Assert.That(ListingStateMachine.CanTransitionCollection(from, to), Is.EqualTo(allowed));
    }
}
