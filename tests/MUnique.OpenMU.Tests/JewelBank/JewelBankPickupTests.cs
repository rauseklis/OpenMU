// <copyright file="JewelBankPickupTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.JewelBank;

using System.Threading;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Tests the auto-deposit of picked-up items: which items the bank recognizes, and that a drop is credited at most once
/// and stays untouched when the credit is refused.
/// </summary>
[TestFixture]
public class JewelBankPickupTests
{
    /// <summary>
    /// Plain jewels and fruits are recognized by their definition and level.
    /// </summary>
    [TestCase((byte)14, (short)13, (byte)0, "Jewel of Bless")]
    [TestCase((byte)12, (short)15, (byte)0, "Jewel of Chaos")]
    [TestCase((byte)13, (short)15, (byte)3, "Fruit of Strength")]
    public void PlainJewelsAndFruitsAreRecognized(byte group, short number, byte level, string expected)
    {
        var item = CreateItem(group, number, level);

        Assert.That(JewelBankPickup.FindKind(item)?.Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// Anything which is not a plain jewel or fruit is left to the normal pickup.
    /// </summary>
    [Test]
    public void OtherItemsAreNotRecognized()
    {
        Assert.That(JewelBankPickup.FindKind(CreateItem(14, 13, 1)), Is.Null, "upgraded jewel");
        Assert.That(JewelBankPickup.FindKind(CreateItem(13, 15, 5)), Is.Null, "fruit level out of range");
        Assert.That(JewelBankPickup.FindKind(CreateItem(0, 0, 0)), Is.Null, "a sword");
        Assert.That(JewelBankPickup.FindKind(CreateItem(14, 13, 0, durability: 0)), Is.Null, "broken");
        Assert.That(JewelBankPickup.FindKind(CreateItem(14, 13, 0, socketCount: 1)), Is.Null, "socketed");
        Assert.That(JewelBankPickup.FindKind(CreateItem(14, 13, 0, durability: 3)), Is.Null, "a possible stack");
    }

    /// <summary>
    /// A drop which is credited is consumed: a second player cannot take it any more.
    /// </summary>
    [Test]
    public async Task CreditedDropIsConsumedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var drop = new DroppedItem(CreateItem(14, 13, 0), new Point(100, 100), player.CurrentMap!, null, null);
        var credits = 0;

        var first = await drop.TryPickUpIntoBankAsync(player, () => { credits++; return ValueTask.FromResult(true); }).ConfigureAwait(false);
        var second = await drop.TryPickUpIntoBankAsync(player, () => { credits++; return ValueTask.FromResult(true); }).ConfigureAwait(false);

        Assert.That(first, Is.True);
        Assert.That(second, Is.False);
        Assert.That(credits, Is.EqualTo(1));
    }

    /// <summary>
    /// A refused credit (for example a full bank) leaves the drop where it is, so it can still be picked up normally.
    /// </summary>
    [Test]
    public async Task RefusedCreditLeavesTheDropAvailableAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var drop = new DroppedItem(CreateItem(14, 13, 0), new Point(100, 100), player.CurrentMap!, null, null);

        var refused = await drop.TryPickUpIntoBankAsync(player, () => ValueTask.FromResult(false)).ConfigureAwait(false);
        var retry = await drop.TryPickUpIntoBankAsync(player, () => ValueTask.FromResult(true)).ConfigureAwait(false);

        Assert.That(refused, Is.False);
        Assert.That(retry, Is.True, "the drop was not consumed by the refused credit");
    }

    /// <summary>
    /// Several players racing for one drop credit it exactly once.
    /// </summary>
    [Test]
    public async Task ConcurrentPickupsCreditTheDropOnceAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var drop = new DroppedItem(CreateItem(14, 13, 0), new Point(100, 100), player.CurrentMap!, null, null);
        var credits = 0;

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
            await drop.TryPickUpIntoBankAsync(player, async () =>
            {
                await Task.Delay(5).ConfigureAwait(false);
                Interlocked.Increment(ref credits);
                return true;
            }).ConfigureAwait(false)))).ConfigureAwait(false);

        Assert.That(results.Count(r => r), Is.EqualTo(1));
        Assert.That(credits, Is.EqualTo(1));
    }

    /// <summary>
    /// Without a Jewel Bank database the pickup is not touched: the item is not taken and the normal pickup goes on.
    /// </summary>
    [Test]
    public async Task WithoutADatabaseNothingIsBankedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var drop = new DroppedItem(CreateItem(14, 13, 0), new Point(100, 100), player.CurrentMap!, null, null);

        var banked = await JewelBankPickup.TryDepositAsync(player, drop).ConfigureAwait(false);

        Assert.That(banked, Is.False);
        Assert.That(await drop.TryPickUpIntoBankAsync(player, () => ValueTask.FromResult(true)).ConfigureAwait(false), Is.True, "the drop is untouched");
    }

    /// <summary>
    /// Verifies the limiter still refuses an account inside the interval after it forgot thousands of other, long idle accounts.
    /// </summary>
    [Test]
    public void RateLimiterKeepsLimitingAfterPruningIdleAccounts()
    {
        var now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        var limiter = new JewelBankRateLimiter(() => now, TimeSpan.FromMilliseconds(250));
        for (var i = 0; i < 3000; i++)
        {
            limiter.TryAcquire(Guid.NewGuid());
        }

        now = now.AddSeconds(10);
        var account = Guid.NewGuid();
        Assert.That(limiter.TryAcquire(account), Is.True);
        Assert.That(limiter.TryAcquire(account), Is.False);
        now = now.AddMilliseconds(300);
        Assert.That(limiter.TryAcquire(account), Is.True);
    }

    /// <summary>
    /// The rate limiter lets the first request pass and refuses a second one inside the interval, per account.
    /// </summary>
    [Test]
    public void RateLimiterRefusesRequestsInsideTheInterval()
    {
        var now = DateTimeOffset.UtcNow;
        var limiter = new JewelBankRateLimiter(() => now, TimeSpan.FromMilliseconds(250));
        var account = Guid.NewGuid();

        Assert.That(limiter.TryAcquire(account), Is.True);
        Assert.That(limiter.TryAcquire(account), Is.False);
        Assert.That(limiter.TryAcquire(Guid.NewGuid()), Is.True, "another account has its own budget");
        now += TimeSpan.FromMilliseconds(251);
        Assert.That(limiter.TryAcquire(account), Is.True);
    }

    private static Item CreateItem(byte group, short number, byte level, double durability = 1, int socketCount = 0)
    {
        return new Item
        {
            Definition = new ItemDefinition { Group = group, Number = number },
            Level = level,
            Durability = durability,
            SocketCount = socketCount,
        };
    }
}
