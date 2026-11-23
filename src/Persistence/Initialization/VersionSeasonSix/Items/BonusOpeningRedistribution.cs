// <copyright file="BonusOpeningRedistribution.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Sets the owner-approved opening chances of the small monster bonuses (Silver Medal, Gold Medal, Heart of Love,
/// Firecracker, Box of Heaven) and the monster chances of Gold Medal (0.35%), Heart of Love (0.30%) and Box of Heaven (0.05%). Rings and Zen keep their existing chances; every listed jewel gets 1%; the rest is
/// shared between the equipment categories as described on the individual methods.
/// </summary>
public sealed class BonusOpeningRedistribution(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const byte WrapperGroup = 14;
    private const short WrapperNumber = 11;

    private const byte FirecrackerLevel = 2;
    private const byte HeartLevel = 3;
    private const byte SilverLevel = 5;
    private const byte GoldLevel = 6;
    private const byte HeavenLevel = 7;

    private const double ChancePerJewel = .01;
    private const double MedalShieldChance = .10;
    private const double MedalWeaponChance = .20;
    private const double HeartWeaponShare = .90;
    private const double FirecrackerArmorShare = .80;
    private const double HeavenHigherPetChance = .03;
    private const double HeavenNormalPetChance = .32;
    private const double HeavenMonsterChance = .0005;
    private const double GoldMonsterChance = .0035;
    private const double HeartMonsterChance = .003;

    /// <inheritdoc />
    public override void Initialize()
    {
        var wrapper = this.GameConfiguration.Items.Single(item => item.Group == WrapperGroup && item.Number == WrapperNumber);

        // Silver and Gold: weapons 20% and shields 10% of the whole opening, armor gets what remains.
        foreach (var level in new[] { SilverLevel, GoldLevel })
        {
            var groups = Groups(wrapper, level);
            SetJewels(groups);
            var remainder = 1 - Total(groups, "jewels") - Total(groups, "rings") - Total(groups, "zen");
            SetCategory(groups, IsWeapon, MedalWeaponChance);
            SetCategory(groups, IsShield, MedalShieldChance);
            SetCategory(groups, IsArmor, remainder - MedalWeaponChance - MedalShieldChance);
            EnsureTotal(groups, level);
        }

        // Heart of Love: weapons 90% / shield 10% of what remains after jewels and Zen.
        var heart = Groups(wrapper, HeartLevel);
        SetJewels(heart);
        var heartRemainder = 1 - Total(heart, "jewels") - Total(heart, "zen");
        SetCategory(heart, IsWeapon, heartRemainder * HeartWeaponShare);
        SetCategory(heart, IsShield, heartRemainder - (heartRemainder * HeartWeaponShare));
        EnsureTotal(heart, HeartLevel);

        // Firecracker: armor 80% / weapons 20% of what remains after jewels and Zen.
        var firecracker = Groups(wrapper, FirecrackerLevel);
        SetJewels(firecracker);
        var firecrackerRemainder = 1 - Total(firecracker, "jewels") - Total(firecracker, "zen");
        SetCategory(firecracker, IsArmor, firecrackerRemainder * FirecrackerArmorShare);
        SetCategory(firecracker, IsWeapon, firecrackerRemainder - (firecrackerRemainder * FirecrackerArmorShare));
        EnsureTotal(firecracker, FirecrackerLevel);

        // Box of Heaven: every higher pet 1%; the freed chance goes to the ordinary pets.
        var heaven = Groups(wrapper, HeavenLevel);
        SetByName(heaven, "higher pets", HeavenHigherPetChance);
        SetByName(heaven, "pets", HeavenNormalPetChance);
        EnsureTotal(heaven, HeavenLevel);

        this.SetMonsterChances();
    }

    private static bool IsArmor(ItemDefinition item) => item.Group is >= 7 and <= 11;

    private static bool IsWeapon(ItemDefinition item) => item.Group < 6 && !(item.Group == 2 && item.Number == 22);

    private static bool IsShield(ItemDefinition item) => item.Group == 6;

    private static string Category(ItemDropItemGroup group)
    {
        var description = group.Description.Value ?? string.Empty;
        var index = description.LastIndexOf(" / ", StringComparison.Ordinal);
        return index < 0 ? description : description[(index + 3)..];
    }

    private static ItemDropItemGroup[] Groups(ItemDefinition wrapper, byte level) =>
        wrapper.DropItems.Where(group => group.SourceItemLevel == level).ToArray();

    private static double Total(IEnumerable<ItemDropItemGroup> groups, string category) =>
        groups.Where(group => Category(group) == category).Sum(group => group.Chance);

    private static void SetJewels(IEnumerable<ItemDropItemGroup> groups)
    {
        foreach (var group in groups.Where(group => Category(group) == "jewels"))
        {
            group.Chance = group.PossibleItems.Count * ChancePerJewel;
        }
    }

    private static void SetByName(IEnumerable<ItemDropItemGroup> groups, string category, double chance)
    {
        var selected = groups.Where(group => Category(group) == category).ToArray();
        if (selected.Length != 1)
        {
            throw new InvalidOperationException($"Expected exactly one '{category}' group, found {selected.Length}.");
        }

        selected[0].Chance = chance;
    }

    /// <summary>
    /// Gives the equipment groups of one category the target total while keeping their internal proportions.
    /// </summary>
    private static void SetCategory(IEnumerable<ItemDropItemGroup> allGroups, Func<ItemDefinition, bool> selector, double target)
    {
        var groups = allGroups.Where(group => group.PossibleItems.Count > 0 && group.PossibleItems.All(selector)).ToArray();
        if (groups.Length == 0)
        {
            throw new InvalidOperationException("Missing equipment category in a bonus box.");
        }

        var current = groups.Sum(group => group.Chance);
        if (current <= 0)
        {
            throw new InvalidOperationException("An equipment category has no chance to scale.");
        }

        var factor = target / current;
        foreach (var group in groups)
        {
            group.Chance *= factor;
        }

        // Keep the rounding residue inside the category.
        groups[^1].Chance = target - groups.Take(groups.Length - 1).Sum(group => group.Chance);
    }

    private static void EnsureTotal(IEnumerable<ItemDropItemGroup> groups, byte level)
    {
        var total = groups.Sum(group => group.Chance);
        if (Math.Abs(total - 1) > 1e-9)
        {
            throw new InvalidOperationException($"Bonus box level {level} sums to {total:R} instead of 1.");
        }
    }

    private void SetMonsterChances()
    {
        var eventRewards = this.GameConfiguration.MiniGameDefinitions.SelectMany(game => game.Rewards)
            .Where(reward => reward.ItemReward is not null).Select(reward => reward.ItemReward!).ToHashSet();
        foreach (var group in this.GameConfiguration.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus
                     && !eventRewards.Contains(group)
                     && group.PossibleItems.Count == 1
                     && group.PossibleItems.Single() is { Group: WrapperGroup, Number: WrapperNumber }))
        {
            switch (group.ItemLevel)
            {
                case GoldLevel:
                    group.Chance = GoldMonsterChance;
                    break;
                case HeartLevel:
                    group.Chance = HeartMonsterChance;
                    break;
                case HeavenLevel:
                    group.Chance = HeavenMonsterChance;
                    break;
            }
        }
    }
}
