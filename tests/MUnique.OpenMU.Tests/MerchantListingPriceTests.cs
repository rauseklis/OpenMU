// <copyright file="MerchantListingPriceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests explicit NPC prices without changing ordinary pricing or copied inventory items.
/// </summary>
[TestFixture]
public class MerchantListingPriceTests
{
    /// <summary>
    /// Only valid listing prices override the usual purchase calculation.
    /// </summary>
    /// <param name="listingPrice">The listing's explicit price.</param>
    [TestCase(null)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1000)]
    public void ListingPriceIsScopedToNpcPurchases(int? listingPrice)
    {
        var itemMock = new Mock<Item>();
        itemMock.SetupAllProperties();
        itemMock.Setup(item => item.ItemOptions).Returns(new List<ItemOptionLink>());
        itemMock.Setup(item => item.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        var item = itemMock.Object;
        item.Definition = new ItemDefinition { Group = 14, Number = 11, Width = 1, Height = 1, Value = 100 };
        item.Durability = 1;
        var calculator = new ItemPriceCalculator();
        var normalPurchase = calculator.CalculateFinalBuyingPrice(item);
        var normalSale = calculator.CalculateSellingPrice(item);
        item.StorePrice = listingPrice;
        Assert.That(calculator.CalculateMerchantBuyingPrice(item), Is.EqualTo(listingPrice is >= 0 ? listingPrice.Value : normalPurchase));
        Assert.That(calculator.CalculateFinalBuyingPrice(item), Is.EqualTo(normalPurchase));
        Assert.That(calculator.CalculateSellingPrice(item), Is.EqualTo(normalSale));
        var purchased = new Item();
        purchased.AssignValues(item);
        Assert.That(purchased.StorePrice, Is.Null, "The NPC listing override must not follow the purchased item.");
    }
}
