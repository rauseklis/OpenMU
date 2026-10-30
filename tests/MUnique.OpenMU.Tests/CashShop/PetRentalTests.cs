// <copyright file="PetRentalTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.CashShop;

using System.Buffers.Binary;
using System.IO;
using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Pet;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.MessageHandler.CashShop;
using MUnique.OpenMU.GameServer.RemoteView.Inventory;
using MUnique.OpenMU.Persistence.CashShop;

/// <summary>Real catalog duration parsing, item delivery and server expiry.</summary>
[TestFixture]
public class PetRentalTests
{
    [Test]
    public void EveryCatalogPetUsesActualProductDuration()
    {
        var assembly = typeof(CashShopCatalog).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".IBSPackage.txt")))!, System.Text.Encoding.Latin1);
        var durations = new HashSet<int>();
        foreach (var line in reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.TrimEnd('\r').Split('@');
            Assert.That(CashShopCatalog.Default.TryResolve(int.Parse(f[2]), int.Parse(f[0]), 0, out var offer), Is.True);
            if (offer.ItemGroup == 13 && offer.ItemNumber is 64 or 65 or 80 or 106 or 123)
            {
                Assert.That(offer.RentalDurationSeconds, Is.GreaterThan(0));
                durations.Add(offer.RentalDurationSeconds!.Value);
            }
        }

        Assert.That(durations, Does.Contain(86400));
        // Explicitly resolve every price option of Demon, rather than just package defaults.
        var demonDurations = new[] { 31, 32, 33 }.Select(price =>
        {
            Assert.That(CashShopCatalog.Default.TryResolve(102, 23, price, out var offer), Is.True);
            return offer.RentalDurationSeconds;
        }).ToArray();
        Assert.That(demonDurations, Is.EqualTo(new int?[] { 86400, 604800, 2592000 }));
    }

    [Test]
    public void NewPetOfferWithoutKnownDurationIsRefused()
    {
        string Read(string suffix)
        {
            var assembly = typeof(CashShopCatalog).Assembly;
            using var reader = new StreamReader(assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + suffix)))!, System.Text.Encoding.Latin1);
            return reader.ReadToEnd();
        }

        var products = string.Join('\n', Read("IBSProduct.txt").Split('\n').Where(l => l.Trim().Length > 0 && l.TrimEnd('\r').Split('@')[14] != "10"));
        var catalog = CashShopCatalog.Parse(Read("IBSCategory.txt"), Read("IBSPackage.txt"), products, Read("cashshop-items.csv"));
        Assert.That(catalog.TryResolve(102, 23, 31, out _), Is.False);
    }

    [TestCase(64)] [TestCase(65)] [TestCase(80)] [TestCase(106)] [TestCase(123)]
    public async Task PurchasedAndNormalVersionsHaveDifferentWear(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var normal = PetDurabilityBalanceTests.CreatePet(number);
        player.GameContext.Configuration.Items.Add(normal.Definition!);
        var before = DateTimeOffset.UtcNow;
        var stored = new CashShopStorageItem(1, 102, 20, 31, 13, number, 0, 1, 86400);
        Assert.That(await CashShopItemDelivery.TryDeliverAsync(player, stored), Is.True);
        var purchased = player.Inventory!.Items.Single(i => i.Definition?.Number == number);
        Assert.That(purchased.RentalExpiresAt, Is.InRange(before.AddDays(1), DateTimeOffset.UtcNow.AddDays(1)));
        Assert.That(PetDurabilityPolicy.DamageLoss(purchased, 180000), Is.Zero);
        await PetDurabilityPolicy.ApplyLossAsync(player, purchased, 1000);
        Assert.That(purchased.Durability, Is.EqualTo(255));
        Assert.That(PetDurabilityPolicy.DamageLoss(normal, 180000), Is.GreaterThan(0));
    }

    [Test]
    public async Task DeliveryCountsAsDoneWhenOnlyTheClientNotificationFails()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        player.GameContext.Configuration.Items.Add(PetDurabilityBalanceTests.CreatePet(64).Definition!);
        var view = player.ViewPlugIns.GetPlugIn<IItemAppearPlugIn>()!;
        Mock.Get(view).Setup(v => v.ItemAppearAsync(It.IsAny<Item>())).ThrowsAsync(new InvalidOperationException("client gone"));

        // The caller restores the storage entry when this is false; the item is in the inventory, so that would duplicate it.
        var delivered = await CashShopItemDelivery.TryDeliverAsync(player, new CashShopStorageItem(1, 102, 20, 31, 13, 64, 0, 1));

        Assert.That(delivered, Is.True);
        Assert.That(player.Inventory!.Items.Count(i => i.Definition?.Number == 64), Is.EqualTo(1));
    }

    [Test]
    public async Task LegacyStorageEntryDoesNotGetGuessedExpiry()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        player.GameContext.Configuration.Items.Add(PetDurabilityBalanceTests.CreatePet(64).Definition!);
        Assert.That(await CashShopItemDelivery.TryDeliverAsync(player, new CashShopStorageItem(1, 102, 20, 31, 13, 64, 0, 1)), Is.True);
        Assert.That(player.Inventory!.Items.Single().RentalExpiresAt, Is.Null);
    }

    [Test]
    public async Task ExpiredEquippedPetIsRemovedAndPermanentPetSurvives()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var rental = PetDurabilityBalanceTests.CreatePet(64);
        rental.RentalExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var permanent = PetDurabilityBalanceTests.CreatePet(64);
        await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, rental);
        await player.Inventory.AddItemAsync(12, permanent);
        rental.RentalExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        Assert.That(PetDurabilityPolicy.IsUsable(rental), Is.False);
        Assert.That(player.GameContext.ItemPowerUpFactory.GetPowerUps(rental, player.Attributes!), Is.Empty);
        await PetRentalLifetime.ExpireAsync(player);
        Assert.That(player.Inventory.Items, Does.Not.Contain(rental));
        Assert.That(player.Inventory.Items, Does.Contain(permanent));
        Assert.That(await player.Inventory.AddItemAsync(InventoryConstants.PetSlot, rental), Is.False);
    }

    [Test]
    public async Task ExpirySchedulerRunsWithoutCombatIncludingOfflineHelper()
    {
        var context = GameContextTestHelper.CreateGameContext();
        await using var player = await PlayerTestHelper.CreateOfflineLevelingPlayerAsync(context);
        var rental = PetDurabilityBalanceTests.CreatePet(80);
        rental.RentalExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(100);
        await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, rental);
        await Task.Delay(350);
        Assert.That(player.Inventory.Items, Does.Not.Contain(rental));
    }

    [Test]
    public async Task RestartUsesPersistedTimestampWithoutRenewingRental()
    {
        await using var first = await PlayerTestHelper.CreatePlayerAsync();
        var rental = PetDurabilityBalanceTests.CreatePet(64);
        rental.RentalExpiresAt = DateTimeOffset.UtcNow.AddHours(-2);
        // Reconstructed item from persistent data is already expired before login.
        first.SelectedCharacter!.Inventory!.Items.Add(rental);
        var character = first.SelectedCharacter;
        await first.SetSelectedCharacterAsync(null);
        await first.SetSelectedCharacterAsync(character);
        Assert.That(first.Inventory!.Items, Does.Not.Contain(rental));
    }

    [Test]
    public void ExistingClientCountdownPacketUsesUtcExpiryAndSlot()
    {
        var item = PetDurabilityBalanceTests.CreatePet(64);
        item.ItemSlot = 8;
        item.RentalExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1793280000);
        var packet = PetRentalPackets.ItemPacket(item);
        Assert.That(packet[..4], Is.EqualTo(new byte[] { 0xC1, 12, 0xD2, 0x12 }));
        Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4)), Is.EqualTo(13 * 512 + 64));
        Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6)), Is.EqualTo(8));
        Assert.That(BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8)), Is.EqualTo(1793280000));
    }

    [Test]
    public void RentalUsesExistingItemExpirationFlagWhilePermanentPetDoesNot()
    {
        var item = PetDurabilityBalanceTests.CreatePet(64);
        var serializer = new GameServer.RemoteView.ItemSerializer();
        var bytes = new byte[serializer.NeededSpace];
        serializer.SerializeItem(bytes, item);
        Assert.That(bytes[5] & 0x06, Is.Zero);
        item.RentalExpiresAt = DateTimeOffset.UtcNow.AddDays(1);
        serializer.SerializeItem(bytes, item);
        Assert.That(bytes[5] & 0x06, Is.EqualTo(0x02));
    }
}
