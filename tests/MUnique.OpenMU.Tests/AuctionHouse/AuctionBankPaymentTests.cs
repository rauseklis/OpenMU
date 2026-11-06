// <copyright file="AuctionBankPaymentTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// Verifies how jewel prices are paid from the Jewel Bank balances and how the bank maps onto the Auction House currencies.
/// </summary>
[TestFixture]
public class AuctionBankPaymentTests
{
    /// <summary>
    /// Loose jewels pay a price exactly, with nothing else touched.
    /// </summary>
    [Test]
    public void LooseJewelsPayExactly()
    {
        var plan = BankJewelPaymentPlanner.Plan(2, 5, [3, 0, 0]);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.LooseUsed, Is.EqualTo(2));
        Assert.That(plan.PacksUsed, Is.EqualTo(new long[] { 0, 0, 0 }));
        Assert.That(plan.ChangeUnits, Is.EqualTo(0));
    }

    /// <summary>
    /// A shortfall of loose jewels is covered by a bundle, and the surplus comes back as change.
    /// </summary>
    [Test]
    public void ShortfallIsCoveredByABundleWithChange()
    {
        var plan = BankJewelPaymentPlanner.Plan(15, 5, [1, 0, 0]);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan!.LooseUsed, Is.EqualTo(5));
        Assert.That(plan.PacksUsed, Is.EqualTo(new long[] { 1, 0, 0 }));
        Assert.That(plan.ChangeUnits, Is.EqualTo(0));

        var withChange = BankJewelPaymentPlanner.Plan(12, 0, [0, 1, 0]);
        Assert.That(withChange, Is.Not.Null);
        Assert.That(withChange!.PacksUsed, Is.EqualTo(new long[] { 0, 1, 0 }));
        Assert.That(withChange.ChangeUnits, Is.EqualTo(8));
    }

    /// <summary>
    /// The units paid, less the change, always equal the price.
    /// </summary>
    /// <param name="price">The price.</param>
    /// <param name="loose">The loose jewels held.</param>
    /// <param name="small">The bundles of 10 held.</param>
    /// <param name="medium">The bundles of 20 held.</param>
    /// <param name="large">The bundles of 30 held.</param>
    [TestCase(1, 0, 0, 0, 1)]
    [TestCase(10, 3, 1, 0, 0)]
    [TestCase(25, 0, 1, 1, 0)]
    [TestCase(45, 2, 0, 0, 2)]
    [TestCase(7, 0, 0, 0, 1)]
    public void PaidUnitsLessChangeEqualThePrice(long price, long loose, long small, long medium, long large)
    {
        var plan = BankJewelPaymentPlanner.Plan(price, loose, [small, medium, large]);

        Assert.That(plan, Is.Not.Null);
        var paid = plan!.LooseUsed + (plan.PacksUsed[0] * 10) + (plan.PacksUsed[1] * 20) + (plan.PacksUsed[2] * 30);
        Assert.That(paid - plan.ChangeUnits, Is.EqualTo(price));
        Assert.That(plan.LooseUsed, Is.LessThanOrEqualTo(loose));
        Assert.That(plan.PacksUsed[0], Is.LessThanOrEqualTo(small));
        Assert.That(plan.PacksUsed[1], Is.LessThanOrEqualTo(medium));
        Assert.That(plan.PacksUsed[2], Is.LessThanOrEqualTo(large));
    }

    /// <summary>
    /// A bank which holds too little is refused, and so is a non-positive price.
    /// </summary>
    [Test]
    public void InsufficientBalanceIsRefused()
    {
        Assert.That(BankJewelPaymentPlanner.Plan(11, 0, [1, 0, 0]), Is.Null);
        Assert.That(BankJewelPaymentPlanner.Plan(3, 2, [0, 0, 0]), Is.Null);
        Assert.That(BankJewelPaymentPlanner.Plan(0, 5, [0, 0, 0]), Is.Null);
        Assert.That(BankJewelPaymentPlanner.Plan(-4, 5, [0, 0, 0]), Is.Null);
    }

    /// <summary>
    /// Every Auction House jewel currency maps to its own loose bank kind and the bundle kinds of the same jewel.
    /// </summary>
    [Test]
    public void JewelCurrenciesMapToTheirBankKinds()
    {
        var seen = new HashSet<byte>();
        for (byte currency = 1; currency <= 7; currency++)
        {
            var code = AuctionBankWallet.JewelCode(currency);
            Assert.That(code, Is.Not.Null, $"currency {currency}");
            Assert.That(seen.Add(code!.Value), Is.True);
            var loose = JewelBankKinds.FindByCode(code.Value)!.Value;
            Assert.That(loose.IsPack, Is.False);
            for (var size = 0; size < 3; size++)
            {
                var pack = JewelBankKinds.FindByCode(AuctionBankWallet.PackCode(code.Value, size))!.Value;
                Assert.That(pack.IsPack, Is.True);
                Assert.That(pack.PackUnits, Is.EqualTo(JewelPackRules.GetPackUnits(size)));
                Assert.That(pack.Name, Does.StartWith(loose.Name.Replace("Jewel of ", string.Empty, StringComparison.Ordinal).Split(' ')[0]));
            }
        }

        Assert.That(AuctionBankWallet.JewelCode(0), Is.Null);
        Assert.That(AuctionBankWallet.JewelCode(8), Is.Null);
    }

    /// <summary>
    /// Each fruit maps to the bank kind of the same fruit.
    /// </summary>
    [TestCase(FruitComponent.Strength, "Fruit of Strength")]
    [TestCase(FruitComponent.Agility, "Fruit of Agility")]
    [TestCase(FruitComponent.Vitality, "Fruit of Vitality")]
    [TestCase(FruitComponent.Energy, "Fruit of Energy")]
    [TestCase(FruitComponent.Command, "Fruit of Command")]
    public void FruitsMapToTheirBankKinds(FruitComponent component, string name)
    {
        Assert.That(JewelBankKinds.FindByCode(AuctionBankWallet.FruitCode(component))!.Value.Name, Is.EqualTo(name));
    }

    /// <summary>
    /// The wallet adds loose jewels and bundles into the total the window shows, and reads the fruits per type.
    /// </summary>
    [Test]
    public void WalletSumsTheBank()
    {
        var bless = AuctionBankWallet.JewelCode(2)!.Value;
        var counts = new Dictionary<byte, long>
        {
            [bless] = 2,
            [AuctionBankWallet.PackCode(bless, 0)] = 1,
            [AuctionBankWallet.PackCode(bless, 2)] = 2,
            [AuctionBankWallet.FruitCode(FruitComponent.Strength)] = 15,
        };
        var state = new JewelBankState(counts, false);

        var (loose, packs, total) = AuctionBankWallet.Jewels(state, 2);
        Assert.That(loose, Is.EqualTo(2));
        Assert.That(packs, Is.EqualTo(new long[] { 1, 0, 2 }));
        Assert.That(total, Is.EqualTo(2 + 10 + 60));
        Assert.That(AuctionBankWallet.Fruits(state).Strength, Is.EqualTo(15));
        Assert.That(AuctionBankWallet.Jewels(state, 1).Total, Is.EqualTo(0));
    }
}
