// <copyright file="VaultMoneyTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests that the vault can hold more Zen than a 32 bit integer allows.
/// </summary>
[TestFixture]
public class VaultMoneyTests
{
    private const long VaultCap = 1_000_000_000_000L;

    /// <summary>
    /// Tests that repeated deposits can raise the vault above the 32 bit limit.
    /// </summary>
    [Test]
    public async ValueTask RepeatedDepositsExceedInt32LimitAsync()
    {
        var player = await CreatePlayerWithVaultAsync().ConfigureAwait(false);

        for (var i = 0; i < 3; i++)
        {
            player.Money = int.MaxValue;
            Assert.That(player.TryDepositVaultMoney(int.MaxValue), Is.True);
        }

        Assert.That(player.Vault!.ItemStorage.Money, Is.EqualTo(3L * int.MaxValue));
        Assert.That(player.Money, Is.EqualTo(0));
    }

    /// <summary>
    /// Tests that the vault is capped at one trillion.
    /// </summary>
    [Test]
    public async ValueTask DepositIsRejectedAboveCapAsync()
    {
        var player = await CreatePlayerWithVaultAsync().ConfigureAwait(false);
        player.Vault!.ItemStorage.Money = VaultCap - 100;
        player.Money = 1000;

        Assert.That(player.TryDepositVaultMoney(101), Is.False);
        Assert.That(player.Vault.ItemStorage.Money, Is.EqualTo(VaultCap - 100));
        Assert.That(player.Money, Is.EqualTo(1000));

        Assert.That(player.TryDepositVaultMoney(100), Is.True);
        Assert.That(player.Vault.ItemStorage.Money, Is.EqualTo(VaultCap));
    }

    /// <summary>
    /// Tests that withdrawing from a vault above the 32 bit limit works and keeps the rest.
    /// </summary>
    [Test]
    public async ValueTask WithdrawFromLargeVaultAsync()
    {
        var player = await CreatePlayerWithVaultAsync().ConfigureAwait(false);
        player.Vault!.ItemStorage.Money = 5_000_000_000L;
        player.Money = 0;

        Assert.That(player.TryTakeVaultMoney(2_000_000_000), Is.True);
        Assert.That(player.Money, Is.EqualTo(2_000_000_000));
        Assert.That(player.Vault.ItemStorage.Money, Is.EqualTo(3_000_000_000L));
    }

    /// <summary>
    /// Tests that withdrawing is rejected when the inventory would exceed its maximum.
    /// </summary>
    [Test]
    public async ValueTask WithdrawIsRejectedWhenInventoryWouldOverflowAsync()
    {
        var player = await CreatePlayerWithVaultAsync().ConfigureAwait(false);
        player.Vault!.ItemStorage.Money = 5_000_000_000L;
        player.Money = int.MaxValue - 10;

        Assert.That(player.TryTakeVaultMoney(11), Is.False);
        Assert.That(player.Vault.ItemStorage.Money, Is.EqualTo(5_000_000_000L));
    }

    /// <summary>
    /// Tests that negative amounts (e.g. from an unsigned client value above int.MaxValue) are rejected.
    /// </summary>
    [Test]
    public async ValueTask NegativeAmountsAreRejectedAsync()
    {
        var player = await CreatePlayerWithVaultAsync().ConfigureAwait(false);
        player.Vault!.ItemStorage.Money = 5_000_000_000L;
        player.Money = 1000;

        Assert.That(player.TryDepositVaultMoney(-1_000), Is.False);
        Assert.That(player.TryTakeVaultMoney(-1_000), Is.False);
        Assert.That(player.Vault.ItemStorage.Money, Is.EqualTo(5_000_000_000L));
        Assert.That(player.Money, Is.EqualTo(1000));
    }

    private static async ValueTask<Player> CreatePlayerWithVaultAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.GameContext.Configuration.MaximumVaultMoney = VaultCap;

        var itemStorage = new Mock<ItemStorage>();
        itemStorage.SetupAllProperties();
        itemStorage.Setup(i => i.Items).Returns(new List<Item>());
        player.Vault = new Storage(InventoryConstants.WarehouseSize, itemStorage.Object);
        return player;
    }
}
