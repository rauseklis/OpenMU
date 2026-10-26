// <copyright file="ChocolateEndgameRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Configures one mutually exclusive ordinary Chocolate outcome with class-first weights.
/// </summary>
public sealed class ChocolateEndgameRewards : InitializerBase
{
    private const byte EquipmentMinimum = 6;
    private const byte EquipmentMaximum = 9;
    private readonly HashSet<ItemDefinition> _rewardItems = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChocolateEndgameRewards"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public ChocolateEndgameRewards(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.ConfigurePink();
        this.ConfigureBlue();
        this.ResetBox(33);
        this.AddGroup(33, "Chocolate Red / Rare / Royal", 1, [(2, 23)], 0, 0);
        foreach (var item in this._rewardItems)
        {
            foreach (var requirement in item.Requirements.Where(requirement => requirement.Attribute == GameLogic.Attributes.Stats.Level && requirement.MinimumValue == 380).ToArray())
            {
                item.Requirements.Remove(requirement);
            }
        }
    }

    private void ConfigurePink()
    {
        this.ResetBox(32);
        this.AddArmor(32, "Pink", "DW", 22, .08);
        this.AddArmor(32, "Pink", "DK", 17, .04);
        this.AddArmor(32, "Pink", "DK", 21, .04);
        this.AddArmor(32, "Pink", "Elf", 24, .08);
        this.AddArmor(32, "Pink", "MG", 23, .08);
        this.AddArmor(32, "Pink", "DL", 28, .08);
        this.AddArmor(32, "Pink", "SUM", 43, .10);
        this.AddArmor(32, "Pink", "RF", 73, .10);
        this.AddWeapon(32, "Pink", "DW", .30 / 7, [(5, 13), (5, 11)]);
        this.AddWeapon(32, "Pink", "DK", .30 / 7, [(0, 22), (0, 17)]);
        this.AddWeapon(32, "Pink", "Elf", .30 / 7, [(4, 19), (4, 22)]);
        this.AddWeapon(32, "Pink", "MG", .30 / 7, [(0, 25), (0, 21)]);
        this.AddWeapon(32, "Pink", "DL", .30 / 7, [(2, 15), (2, 12)]);
        this.AddWeapon(32, "Pink", "SUM", .30 / 7, [(5, 19)]);
        this.AddWeapon(32, "Pink", "RF", .30 / 7, [(0, 35)]);
        foreach (var (name, number) in new[] { ("DW", 20), ("DK", 17), ("Elf", 19), ("MG", 18), ("DL", 21) })
        {
            this.AddGroup(32, $"Chocolate Pink / Shield / {name} / {number}", .02, [(6, number)]);
        }
    }

    private void ConfigureBlue()
    {
        this.ResetBox(34);
        foreach (var (name, number) in new[] { ("DW", 30), ("DK", 29), ("Elf", 31), ("MG", 32), ("DL", 33), ("SUM", 44) })
        {
            this.AddArmor(34, "Blue", name, number, .70 / 6);
        }

        this.AddWeapon(34, "Blue", "DW", .25 / 6, [(5, 12)]);
        this.AddWeapon(34, "Blue", "DK", .25 / 6, [(0, 24)]);
        this.AddWeapon(34, "Blue", "Elf", .25 / 6, [(4, 20), (4, 21)]);
        this.AddWeapon(34, "Blue", "MG", .25 / 6, [(0, 23)]);
        this.AddWeapon(34, "Blue", "DL", .25 / 6, [(2, 14)]);
        this.AddWeapon(34, "Blue", "SUM", .25 / 6, [(5, 20)]);
        this.AddGroup(34, "Chocolate Blue / Rare / Mace of King", .05, [(2, 22)], 0, 0);
    }

    private void AddArmor(int box, string color, string name, int number, double chance)
    {
        var groups = name == "MG" ? new[] { 8, 9, 10, 11 } : name == "RF" ? new[] { 7, 8, 9, 11 } : new[] { 7, 8, 9, 10, 11 };
        this.AddGroup(box, $"Chocolate {color} / Armor / {name} / {number}", chance, groups.Select(group => (group, number)).ToArray());
    }

    private void AddWeapon(int box, string color, string name, double chance, (int Group, int Number)[] items)
        => this.AddGroup(box, $"Chocolate {color} / Weapon / {name} / weapons", chance, items);

    private void ResetBox(int number)
    {
        var box = this.FindItem(14, number);
        foreach (var group in box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray())
        {
            box.DropItems.Remove(group);
        }
    }

    private void AddGroup(int number, string description, double chance, (int Group, int Number)[] items, byte minimum = EquipmentMinimum, byte maximum = EquipmentMaximum)
    {
        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.SourceItemLevel = 0;
        group.Description = description;
        group.Chance = chance;
        group.ItemType = SpecialItemType.RandomItem;
        group.MinimumLevel = minimum;
        group.MaximumLevel = maximum;
        foreach (var key in items)
        {
            var definition = this.FindItem(key.Group, key.Number);
            group.PossibleItems.Add(definition);
            this._rewardItems.Add(definition);
        }

        this.FindItem(14, number).DropItems.Add(group);
    }

    private ItemDefinition FindItem(int group, int number) => this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
}
