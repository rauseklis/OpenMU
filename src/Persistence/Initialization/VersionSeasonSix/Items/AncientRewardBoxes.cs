// <copyright file="AncientRewardBoxes.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Configures one guaranteed +0 Ancient piece per box, equally weighted across the tier's pieces.
/// </summary>
public sealed class AncientRewardBoxes : InitializerBase
{
    private const byte BoxGroup = 14;
    private const double AncientTenStatChance = 0.30;

    /// <summary>
    /// Initializes a new instance of the <see cref="AncientRewardBoxes"/> class.
    /// </summary>
    public AncientRewardBoxes(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.ConfigureBox(11, "Box of Luck", ["Hyperion", "Barnake", "Fase", "Cloud"]);
        this.ConfigureBox(124, "Silver Box", ["Garuda", "Heras", "Gaia", "Eplete", "Semeden", "Chamer"]);
        this.ConfigureBox(123, "Golden Box", ["Hyon", "Anubis", "Aruan", "Gaion", "Agnis", "Chrono", "Trite"]);
    }

    private void ConfigureBox(short number, string name, string[] setNames)
    {
        var box = this.GameConfiguration.Items.SingleOrDefault(item => item.Group == BoxGroup && item.Number == number);
        if (box is null)
        {
            box = this.Context.CreateNew<ItemDefinition>();
            box.Group = BoxGroup;
            box.Number = number;
            box.Name = name;
            box.Width = 1;
            box.Height = 1;
            box.Durability = 1;
            box.SetGuid(BoxGroup, number);
            box.IsTradable = false;
            box.IsSellableToNpc = false;
            box.IsPersonalStoreSellable = false;
            box.IsRepairable = false;
            this.GameConfiguration.Items.Add(box);
        }

        var sets = setNames.Select(setName => this.GameConfiguration.ItemSetGroups.Single(set => set.Name == setName)).ToList();
        var totalPieces = sets.Sum(set => set.Items.Count);
        foreach (var oldGroup in box.DropItems.Where(group => group.SourceItemLevel == 0).ToList())
        {
            box.DropItems.Remove(oldGroup);
        }

        foreach (var set in sets)
        {
            var group = this.Context.CreateNew<ItemDropItemGroup>();
            group.SourceItemLevel = 0;
            group.Description = $"Ancient tier boxes: {name} / {set.Name}";
            group.ItemType = SpecialItemType.Ancient;
            group.AncientSet = set;
            group.AncientBonusLevelTwoChance = AncientTenStatChance;
            group.Chance = (double)set.Items.Count / totalPieces;
            foreach (var member in set.Items)
            {
                group.PossibleItems.Add(member.ItemDefinition!);
            }

            box.DropItems.Add(group);
        }
    }
}
