// <copyright file="VaultSwitchTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.Vault;

/// <summary>
/// Tests for the multiple vaults of an account.
/// </summary>
[TestFixture]
public class VaultSwitchTests
{
    private static ItemStorage CreateItemStorage()
    {
        var itemStorage = new Mock<ItemStorage>();
        itemStorage.SetupAllProperties();
        itemStorage.Setup(i => i.Items).Returns(new List<Item>());
        return itemStorage.Object;
    }

    /// <summary>
    /// Tests that vault 1 is the account vault and the others are created on demand and found again.
    /// </summary>
    [Test]
    public async ValueTask GetOrCreateVaultStorageAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);

        var first = player.GetOrCreateVaultStorage(1);
        Assert.That(first, Is.SameAs(player.Account!.Vault));

        var third = player.GetOrCreateVaultStorage(3);
        Assert.That(third, Is.Not.SameAs(first));
        Assert.That(third.VaultNumber, Is.EqualTo(3));
        Assert.That(player.Account.ExtraVaults, Does.Contain(third));
        Assert.That(player.GetOrCreateVaultStorage(3), Is.SameAs(third));
        Assert.That(player.GetOrCreateVaultStorage(4), Is.Not.SameAs(third));
        Assert.That(player.Account.ExtraVaults.Count, Is.EqualTo(2));
    }

    /// <summary>
    /// Tests that switching changes the active vault and every vault keeps its own items.
    /// </summary>
    [Test]
    public async ValueTask SwitchingChangesTheActiveVaultAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var action = new SwitchVaultAction();
        var first = player.Vault!.ItemStorage;

        await action.SwitchVaultAsync(player, 5).ConfigureAwait(false);
        Assert.That(player.ActiveVaultNumber, Is.EqualTo(5));
        Assert.That(player.Vault!.ItemStorage.VaultNumber, Is.EqualTo(5));
        Assert.That(player.Vault.ItemStorage, Is.Not.SameAs(first));

        var fifth = player.Vault.ItemStorage;
        await action.SwitchVaultAsync(player, 1).ConfigureAwait(false);
        Assert.That(player.ActiveVaultNumber, Is.EqualTo(1));
        Assert.That(player.Vault!.ItemStorage, Is.SameAs(first));

        await action.SwitchVaultAsync(player, 5).ConfigureAwait(false);
        Assert.That(player.Vault!.ItemStorage, Is.SameAs(fifth));
    }

    /// <summary>
    /// Tests that invalid vault numbers are refused and the active vault stays.
    /// </summary>
    [TestCase((byte)0)]
    [TestCase((byte)11)]
    [TestCase((byte)255)]
    public async ValueTask InvalidVaultNumbersAreRefusedAsync(byte vaultNumber)
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        var first = player.Vault!.ItemStorage;

        await new SwitchVaultAction().SwitchVaultAsync(player, vaultNumber).ConfigureAwait(false);

        Assert.That(player.ActiveVaultNumber, Is.EqualTo(1));
        Assert.That(player.Vault!.ItemStorage, Is.SameAs(first));
        Assert.That(player.Account!.ExtraVaults, Is.Empty);
    }

    /// <summary>
    /// Tests that the Zen balance is shared by all vaults.
    /// </summary>
    [Test]
    public async ValueTask ZenIsSharedByAllVaultsAsync()
    {
        var player = await CreatePlayerAsync().ConfigureAwait(false);
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.GameContext.Configuration.MaximumVaultMoney = 1_000_000_000_000L;
        var first = player.Vault!.ItemStorage;
        var action = new SwitchVaultAction();

        player.Money = 1_000_000;
        Assert.That(player.TryDepositVaultMoney(400_000), Is.True);
        Assert.That(first.Money, Is.EqualTo(400_000));

        await action.SwitchVaultAsync(player, 7).ConfigureAwait(false);
        Assert.That(player.TryDepositVaultMoney(100_000), Is.True);
        Assert.That(first.Money, Is.EqualTo(500_000));
        Assert.That(player.Vault!.ItemStorage.Money, Is.EqualTo(0));

        Assert.That(player.TryTakeVaultMoney(250_000), Is.True);
        Assert.That(first.Money, Is.EqualTo(250_000));
        Assert.That(player.Money, Is.EqualTo(750_000));
    }

    /// <summary>
    /// Tests that switching is ignored when no vault dialog is open.
    /// </summary>
    [Test]
    public async ValueTask SwitchingWithoutOpenVaultIsIgnoredAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);

        await new SwitchVaultAction().SwitchVaultAsync(player, 3).ConfigureAwait(false);

        Assert.That(player.ActiveVaultNumber, Is.EqualTo(1));
        Assert.That(player.Vault, Is.Null);
    }

    private static async ValueTask<Player> CreatePlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var accountMock = Mock.Get(player.Account!);
        var extraVaults = new List<ItemStorage>();
        ItemStorage? vault = null;
        accountMock.SetupGet(a => a.ExtraVaults).Returns(extraVaults);
        accountMock.SetupGet(a => a.Vault).Returns(() => vault);
        accountMock.SetupSet(a => a.Vault = It.IsAny<ItemStorage?>()).Callback<ItemStorage?>(v => vault = v);
        vault = CreateItemStorage();

        player.Vault = new Storage(InventoryConstants.WarehouseSize, vault);
        player.OpenedNpc = new NonPlayerCharacter(null!, new MonsterDefinition { NpcWindow = NpcWindow.VaultStorage }, null!);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false), Is.True);
        return player;
    }
}
