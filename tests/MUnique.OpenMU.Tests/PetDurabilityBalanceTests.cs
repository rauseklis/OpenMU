// <copyright file="PetDurabilityBalanceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Reflection;
using System.Threading;
using Moq;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Pet;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using Item = MUnique.OpenMU.Persistence.BasicModel.Item;
using ItemDefinition = MUnique.OpenMU.Persistence.BasicModel.ItemDefinition;
using ItemSlotType = MUnique.OpenMU.Persistence.BasicModel.ItemSlotType;
using Skill = MUnique.OpenMU.Persistence.BasicModel.Skill;
using SkillEntry = MUnique.OpenMU.Persistence.BasicModel.SkillEntry;
using MasterSkillDefinition = MUnique.OpenMU.Persistence.BasicModel.MasterSkillDefinition;

/// <summary>Approved lifetime calibration and real inventory/command/repair regressions.</summary>
[TestFixture]
public class PetDurabilityBalanceTests
{
    private static readonly short[] PetNumbers = [0, 1, 2, 3, 4, 5, 37, 64, 65, 67, 80, 106, 123];

    private static IEnumerable<TestCaseData> DamageProfiles()
    {
        foreach (var number in PetNumbers.Where(n => n != 5))
        {
            foreach (var hp in new[] { 0.0, 10, 50, 200 })
            {
                yield return new TestCaseData(number, hp);
            }
        }
    }

    [TestCaseSource(nameof(DamageProfiles))]
    public void EveryDamagePetMatchesApprovedFarmingProfiles(short number, double hpPerSecond)
    {
        var pet = CreatePet(number);
        var expectedHours = number is 0 or 1 or 2 or 106 ? 3 : number == 4 ? 72 : number == 37 ? 24 : 6;
        var loss = PetDurabilityPolicy.DamageLoss(pet, hpPerSecond * 3600);
        Assert.That(loss, Is.EqualTo(255.0 / expectedHours * hpPerSecond / 50).Within(1e-10));
        if (hpPerSecond == 50)
        {
            Assert.That(255 / loss, Is.EqualTo(expectedHours).Within(1e-10));
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
    [TestCase(37)] [TestCase(64)] [TestCase(65)] [TestCase(67)] [TestCase(80)] [TestCase(106)] [TestCase(123)]
    public async Task FractionalZeroCrossingDestroysOnlyOrdinaryPets(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(number);
        pet.Durability = 0.0000001;
        pet.Level = 12;
        pet.PetExperience = 12345678;
        await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, pet);
        await PetDurabilityPolicy.ApplyLossAsync(player, pet, 0.000001);
        Assert.That(pet.Durability, Is.Zero);
        Assert.That(player.Inventory.Items.Contains(pet), Is.EqualTo(number is 4 or 37));
        Assert.That(pet.Level, Is.EqualTo(12));
        Assert.That(pet.PetExperience, Is.EqualTo(12345678));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)]
    public async Task EveryFenrirColorSurvivesExhaustionAndTwoJewelRepair(int color)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(37);
        if (color != 0)
        {
            pet.ItemOptions.Add(new Persistence.BasicModel.ItemOptionLink
            {
                ItemOption = new Persistence.BasicModel.IncreasableItemOption
                {
                    OptionType = color == 1 ? ItemOptionTypes.BlackFenrir : color == 2 ? ItemOptionTypes.BlueFenrir : ItemOptionTypes.GoldFenrir,
                },
            });
        }

        var options = pet.ItemOptions.ToArray();
        Assert.That(PetDurabilityPolicy.DamageLoss(pet, 50.0 * 3600 * 24), Is.EqualTo(255).Within(1e-10));
        await player.Inventory!.AddItemAsync(12, pet);
        await PetDurabilityPolicy.ApplyLossAsync(player, pet, 1000);
        Assert.That(player.Inventory.Items.Contains(pet), Is.True);
        var bless = await AddJewel(player, 13, 13);
        var creation = await AddJewel(player, 22, 14);
        var handler = new BlessJewelConsumeHandlerPlugIn();
        Assert.That(await handler.ConsumeItemAsync(player, bless, pet, FruitUsage.Undefined), Is.True);
        Assert.That(pet.Durability, Is.EqualTo(255));
        Assert.That(bless.Durability, Is.Zero);
        Assert.That(player.Inventory.Items.Contains(creation), Is.False);
        Assert.That(pet.ItemOptions, Is.EqualTo(options));
        Assert.That(PetDurabilityPolicy.IsUsable(pet), Is.True);
    }

    [TestCase(false, 100)] [TestCase(true, 255)]
    public async Task InvalidFenrirRepairConsumesNothing(bool hasCreation, double life)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(37); pet.Durability = life;
        await player.Inventory!.AddItemAsync(12, pet);
        var bless = await AddJewel(player, 13, 13);
        var creation = hasCreation ? await AddJewel(player, 22, 14) : null;
        Assert.That(await new BlessJewelConsumeHandlerPlugIn().ConsumeItemAsync(player, bless, pet, FruitUsage.Undefined), Is.False);
        Assert.That(bless.Durability, Is.EqualTo(1));
        Assert.That(creation?.Durability, Is.EqualTo(hasCreation ? (double?)1 : null));
        Assert.That(pet.Durability, Is.EqualTo(life));
    }

    [Test]
    public async Task MissingBlessCannotConsumeCreationOrRepairFenrir()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(37); pet.Durability = 100;
        await player.Inventory!.AddItemAsync(12, pet);
        var absentBless = await AddJewel(player, 13, 13); absentBless.Durability = 0;
        var creation = await AddJewel(player, 22, 14);
        Assert.That(await new BlessJewelConsumeHandlerPlugIn().ConsumeItemAsync(player, absentBless, pet, FruitUsage.Undefined), Is.False);
        Assert.That(creation.Durability, Is.EqualTo(1));
        Assert.That(pet.Durability, Is.EqualTo(100));
    }

    [Test]
    public void RavenCalibrationPreservesExistingCallbackMechanism()
    {
        var raven = CreatePet(5);
        Assert.That(PetDurabilityPolicy.DamageLoss(raven, 180000), Is.Zero, "Raven must never switch to incoming-damage wear.");
        var expectedHourlyLoss = (2 + 1.28) / 3 * 3600 * PetDurabilityPolicy.RavenLoss(raven);
        Assert.That(255 / expectedHourlyLoss, Is.EqualTo(72).Within(1e-10));
        Assert.That(PetDurabilityPolicy.RavenLoss(raven, 20), Is.EqualTo(PetDurabilityPolicy.RavenLoss(raven) * 0.8).Within(1e-12));
    }

    [Test]
    public async Task RavenFinalFractionStopsCommandsWithoutLosingExperience()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var raven = CreatePet(5); raven.Durability = 0.0000001; raven.PetExperience = 7654321; raven.Level = 30;
        await player.Inventory!.AddItemAsync(InventoryConstants.RightHandSlot, raven);
        var manager = new RavenCommandManager(player, raven);
        await PetDurabilityPolicy.ApplyLossAsync(player, raven, PetDurabilityPolicy.RavenLoss(raven));
        Assert.That(raven.Durability, Is.Zero);
        foreach (var mode in new[] { PetBehaviour.AttackWithOwner, PetBehaviour.AttackRandom, PetBehaviour.AttackTarget })
        {
            await manager.SetBehaviourAsync(mode, new Mock<IAttackable>().Object);
            Assert.That(typeof(RavenCommandManager).GetField("_currentBehaviour", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager), Is.EqualTo(PetBehaviour.Idle));
            Assert.That(typeof(RavenCommandManager).GetField("_attackCts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager), Is.Null);
        }

        Assert.That(raven.PetExperience, Is.EqualTo(7654321));
        Assert.That(raven.Level, Is.EqualTo(30));
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        player.TryAddMoney(100_000_000);
        await new ItemRepairAction().RepairItemAsync(player, InventoryConstants.RightHandSlot);
        Assert.That(PetDurabilityPolicy.IsUsable(raven), Is.True);
        await manager.SetBehaviourAsync(PetBehaviour.AttackWithOwner, null);
        Assert.That(typeof(RavenCommandManager).GetField("_attackCts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager), Is.Not.Null);
        manager.Dispose();
    }

    [TestCase(4)] [TestCase(5)]
    public async Task DarkLordPetsRepairWithUnchangedZenPriceAndProgression(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        player.GameContext.Configuration.MaximumInventoryMoney = int.MaxValue;
        var pet = CreatePet(number); pet.Durability = 0; pet.Level = 25; pet.PetExperience = 9876543;
        await player.Inventory!.AddItemAsync(12, pet);
        player.TryAddMoney(100_000_000);
        var before = player.Money;
        var expected = new ItemPriceCalculator().CalculateRepairPrice(pet, false);
        await new ItemRepairAction().RepairItemAsync(player, 12);
        Assert.That(pet.Durability, Is.EqualTo(255));
        Assert.That(before - player.Money, Is.EqualTo(expected));
        Assert.That(pet.Level, Is.EqualTo(25));
        Assert.That(pet.PetExperience, Is.EqualTo(9876543));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(64)] [TestCase(65)] [TestCase(67)] [TestCase(80)] [TestCase(106)] [TestCase(123)]
    public async Task OrdinaryPetsRemainUnrepairable(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(number); pet.Durability = 10;
        await player.Inventory!.AddItemAsync(12, pet);
        player.TryAddMoney(100_000_000);
        await new ItemRepairAction().RepairItemAsync(player, 12);
        Assert.That(pet.Durability, Is.EqualTo(10));
    }

    [TestCase(10, .9)] [TestCase(20, .8)] [TestCase(110, 0)] [TestCase(-10, 1)]
    public void DisplayedPercentIsTheActualProtection(double percent, double multiplier)
        => Assert.That(PetDurabilityPolicy.ProtectionMultiplier(percent), Is.EqualTo(multiplier).Within(1e-12));

    [Test]
    public async Task MasterDurabilityCategoriesDoNotMultiplyAndReadCurrentLevels()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var armor = CreatePet(0); armor.Definition!.Group = 7; armor.ItemSlot = InventoryConstants.ArmorSlot;
        var angel = CreatePet(0);
        var horse = CreatePet(4);
        var weaponSkill = new SkillEntry { Level = 1, Skill = new Skill { Number = 300, MasterDefinition = new MasterSkillDefinition { MaximumLevel = 20, DisplayValueFormula = "10 * level", TargetAttribute = Stats.ItemDurationIncrease } } };
        var petSkill = new SkillEntry { Level = 1, Skill = new Skill { Number = 312, MasterDefinition = new MasterSkillDefinition { MaximumLevel = 20, DisplayValueFormula = "20 * level", TargetAttribute = Stats.ItemDurationIncrease } } };
        player.SelectedCharacter!.LearnedSkills.Add(weaponSkill);
        player.SelectedCharacter.LearnedSkills.Add(petSkill);
        Assert.That(DurabilityProtection.Percent(player, armor), Is.EqualTo(10));
        Assert.That(DurabilityProtection.Percent(player, angel), Is.EqualTo(20));
        Assert.That(DurabilityProtection.Percent(player, horse), Is.Zero);
        petSkill.Level = 2;
        Assert.That(DurabilityProtection.Percent(player, angel), Is.EqualTo(40));
        petSkill.Skill!.MasterDefinition!.ReplacedSkill = weaponSkill.Skill;
        player.SelectedCharacter.LearnedSkills.Clear();
        Assert.That(DurabilityProtection.Percent(player, angel), Is.Zero);
    }

    [TestCase(4)] [TestCase(5)] [TestCase(37)]
    public async Task PlasmaStormCannotHalveProtectedPets(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(number);
        await player.Inventory!.AddItemAsync(number == 5 ? InventoryConstants.RightHandSlot : InventoryConstants.PetSlot, pet);
        Assert.That(PlasmaStormSkillPlugIn.TryReduceEquipmentDurability(pet), Is.False);
        Assert.That(pet.Durability, Is.EqualTo(255));
    }

    [Test]
    public void PlasmaStormPreservesOrdinaryEquipmentHalving()
    {
        var armor = CreatePet(0); armor.Definition!.Group = 7;
        Assert.That(PlasmaStormSkillPlugIn.TryReduceEquipmentDurability(armor), Is.True);
        Assert.That(armor.Durability, Is.EqualTo(127.5));
    }

    [TestCase(4)] [TestCase(5)] [TestCase(37)]
    public async Task RetainedPetsLoseBonusesAtZeroAndRegainThemAfterRepair(short number)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(number);
        pet.Definition!.BasePowerUpAttributes.Add(new Persistence.BasicModel.ItemBasePowerUpDefinition
        {
            TargetAttribute = Stats.MaximumHealth, BaseValue = 50, AggregateType = AttributeSystem.AggregateType.AddRaw,
        });
        var baseline = player.Attributes![Stats.MaximumHealth];
        await player.Inventory!.AddItemAsync(number == 5 ? InventoryConstants.RightHandSlot : InventoryConstants.PetSlot, pet);
        Assert.That(player.Attributes[Stats.MaximumHealth], Is.EqualTo(baseline + 50));
        await PetDurabilityPolicy.ApplyLossAsync(player, pet, 1000);
        Assert.That(player.Attributes[Stats.MaximumHealth], Is.EqualTo(baseline));
        pet.Durability = 255; // The common refresh called by both actual repair paths.
        await PetDurabilityPolicy.RefreshAsync(player, pet);
        Assert.That(player.Attributes[Stats.MaximumHealth], Is.EqualTo(baseline + 50));
    }

    [TestCase(62, 4)] [TestCase(76, 37)]
    public async Task ExhaustedMountCannotUseItsSkill(short skillNumber, short itemNumber)
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var pet = CreatePet(itemNumber); pet.Durability = 0;
        await player.Inventory!.AddItemAsync(InventoryConstants.PetSlot, pet);
        Assert.That(await player.TryConsumeForSkillAsync(new SkillEntry { Skill = new Skill { Number = skillNumber } }), Is.False);
    }

    [Test]
    public async Task DamageHookPreservesUnboostedEquipmentWearAndNoHitCostsNothing()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        player.GameContext.Configuration.DamagePerOneItemDurability = 2000;
        var item = CreatePet(0); item.Definition!.Group = 7;
        var method = typeof(Player).GetMethod("DecreaseDefenseItemDurabilityAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (ValueTask)method.Invoke(player, new object[] { item, new HitInfo(50, 0, DamageAttributes.Undefined) })!;
        Assert.That(item.Durability, Is.EqualTo(255 - 50.0 / 2000).Within(1e-12));
        var before = item.Durability;
        await (ValueTask)method.Invoke(player, new object[] { item, new HitInfo(0, 0, DamageAttributes.Undefined) })!;
        Assert.That(item.Durability, Is.EqualTo(before));
    }

    [Test]
    public async Task ActualReplacedMasterSkillDoesNotRemainEffective()
    {
        await using var player = await PlayerTestHelper.CreatePlayerAsync();
        var item = CreatePet(0); item.Definition!.Group = 7; item.ItemSlot = InventoryConstants.ArmorSlot;
        var previous = new Skill { Number = 300, MasterDefinition = new MasterSkillDefinition { MaximumLevel = 20, DisplayValueFormula = "10 * level" } };
        var replacement = new Skill { Number = 578, MasterDefinition = new MasterSkillDefinition { MaximumLevel = 20, DisplayValueFormula = "20 * level", ReplacedSkill = previous } };
        player.SelectedCharacter!.LearnedSkills.Add(new SkillEntry { Skill = previous, Level = 1 });
        player.SelectedCharacter.LearnedSkills.Add(new SkillEntry { Skill = replacement, Level = 1 });
        Assert.That(DurabilityProtection.Percent(player, item), Is.EqualTo(20));
    }

    internal static Item CreatePet(short number)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Definition = new ItemDefinition
            {
                Group = 13, Number = number, Width = 1, Height = 1, Durability = 255,
                ItemSlot = new ItemSlotType(), IsRepairable = number is 4 or 5,
                PetExperienceFormula = number is 4 or 5 ? "level * level * level * 100 * (level + 10)" : null,
            },
            Durability = 255,
        };
        return item;
    }

    private static async Task<Item> AddJewel(Player player, short number, byte slot)
    {
        var jewel = new Item { Id = Guid.NewGuid(), Definition = new ItemDefinition { Group = 14, Number = number, Width = 1, Height = 1, Durability = 1 }, Durability = 1 };
        await player.Inventory!.AddItemAsync(slot, jewel);
        return jewel;
    }
}
