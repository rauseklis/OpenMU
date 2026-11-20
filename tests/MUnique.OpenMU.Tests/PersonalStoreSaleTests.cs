// <copyright file="PersonalStoreSaleTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Tests the purchase of an item from a personal store.
/// </summary>
[TestFixture]
public class PersonalStoreSaleTests
{
    private const int Price = 300;

    /// <summary>
    /// A normal sale moves the item and the zen, and both players are saved right away, so that the items and the
    /// payment do not drift apart if the server stops soon afterwards.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SaleMovesItemAndZenAndSavesBothPlayersAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        var buyerSaves = buyer.SavesCompleted;
        var sellerSaves = seller.SavesCompleted;

        await new BuyRequestAction().BuyItemAsync(buyer, seller, (byte)InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.That(buyer.Money, Is.EqualTo(1000 - Price));
        Assert.That(seller.Money, Is.EqualTo(500 + Price));
        Assert.That(buyer.Inventory!.Items, Does.Contain(item));
        Assert.That(seller.ShopStorage!.Items, Is.Empty);
        Assert.That(buyer.SavesCompleted, Is.GreaterThan(buyerSaves), "buyer saved after the sale");
        Assert.That(seller.SavesCompleted, Is.GreaterThan(sellerSaves), "seller saved after the sale");
    }

    /// <summary>
    /// An item whose rental ran out can not be added to the inventory of the buyer, so it must not be sold:
    /// nobody is charged and the item stays in the store.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ExpiredRentalItemIsNotSoldAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        item.RentalExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        await new BuyRequestAction().BuyItemAsync(buyer, seller, (byte)InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.That(buyer.Money, Is.EqualTo(1000), "the buyer is not charged");
        Assert.That(seller.Money, Is.EqualTo(500));
        Assert.That(seller.ShopStorage!.Items, Does.Contain(item), "the item stays in the store");
        Assert.That(buyer.Inventory!.Items, Does.Not.Contain(item));
    }

    /// <summary>
    /// A buyer without room for the item is not charged and the item stays in the store.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task BuyerWithoutSpaceIsNotChargedAsync()
    {
        var (buyer, seller, item) = await CreateStoreAsync().ConfigureAwait(false);
        item.Definition!.Width = 8;
        item.Definition.Height = 8;
        for (byte slot = 12; slot < 76; slot++)
        {
            var filler = NewItem();
            if (!await buyer.Inventory!.AddItemAsync(slot, filler).ConfigureAwait(false))
            {
                break;
            }
        }

        await new BuyRequestAction().BuyItemAsync(buyer, seller, (byte)InventoryConstants.FirstStoreItemSlotIndex).ConfigureAwait(false);

        Assert.That(buyer.Money, Is.EqualTo(1000));
        Assert.That(seller.ShopStorage!.Items, Does.Contain(item));
    }

    private static Item NewItem()
    {
        var mock = new Mock<Item>();
        mock.SetupAllProperties();
        mock.Object.Definition = new ItemDefinition { Width = 1, Height = 1, IsPersonalStoreSellable = true };
        mock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        return mock.Object;
    }

    private static async Task<(Player Buyer, Player Seller, Item Item)> CreateStoreAsync()
    {
        var buyer = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var seller = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        buyer.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        seller.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        buyer.Money = 1000;
        seller.Money = 500;

        var item = NewItem();
        item.StorePrice = Price;
        Assert.That(await seller.ShopStorage!.AddItemAsync((byte)InventoryConstants.FirstStoreItemSlotIndex, item).ConfigureAwait(false), Is.True);
        seller.ShopStorage.StoreOpen = true;
        return (buyer, seller, item);
    }
}
