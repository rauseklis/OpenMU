// <copyright file="CherryBlossomRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Configures single-branch Play-Box drops and the three Noria exchanges.
/// </summary>
public sealed class CherryBlossomRewards(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    private const byte BranchGroup = 14;
    private const short SpiritNumber = 450;
    private const int OrdinaryGoldWeight = 9;
    private const int GoldenBoxWeight = 5;

    /// <inheritdoc />
    public override void Initialize()
    {
        this.EnsureBranch(88, "White Cherry Blossom Branch", 10);
        this.EnsureBranch(89, "Red Cherry Blossom Branch", 30);
        this.EnsureBranch(90, "Golden Cherry Blossom Branch", 255);
        this.ConfigurePlayBox();
        var spirit = this.GameConfiguration.Monsters.Single(monster => monster.Number == SpiritNumber);
        foreach (var oldRecipe in spirit.ItemCraftings.Where(recipe => recipe.Number is 41 or 42 or 43).ToArray())
        {
            spirit.ItemCraftings.Remove(oldRecipe);
        }

        this.AddExchange(spirit, 42, 88, 10, [(14, 13), (12, 15), (14, 16), (14, 14), (13, 48), (13, 65)]);
        this.AddExchange(spirit, 43, 89, 30, [(14, 11), (13, 46), (13, 47), (13, 123), (13, 106), (13, 80)]);
        this.AddExchange(spirit, 41, 90, 255, [(13, 47), (13, 46), (12, 30), (12, 31), (14, 124), (14, 123)]);
    }

    private void EnsureBranch(short number, string name, byte capacity)
    {
        var branch = this.GameConfiguration.Items.SingleOrDefault(item => item.Group == BranchGroup && item.Number == number);
        if (branch is null)
        {
            branch = this.Context.CreateNew<ItemDefinition>();
            branch.Group = BranchGroup;
            branch.Number = number;
            branch.Name = name;
            branch.SetGuid(BranchGroup, number);
            this.GameConfiguration.Items.Add(branch);
        }

        branch.Width = 1;
        branch.Height = 2;
        branch.Durability = capacity;
        branch.IsRepairable = false;
    }

    private void ConfigurePlayBox()
    {
        var box = this.Item(14, 84);
        foreach (var old in box.DropItems.Where(group => group.SourceItemLevel == 0).ToArray())
        {
            box.DropItems.Remove(old);
        }

        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = "Cherry Blossom Play-Box";
        group.ItemType = SpecialItemType.RandomItem;
        group.Chance = 1;
        group.DropEffect = ItemDropEffect.Swirl;
        foreach (var number in new short[] { 85, 86, 87, 88, 89, 90 })
        {
            group.PossibleItems.Add(this.Item(14, number));
        }

        box.DropItems.Add(group);
    }

    private void AddExchange(MonsterDefinition spirit, byte recipeNumber, short branchNumber, byte count, (byte Group, short Number)[] rewards)
    {
        var recipe = this.Context.CreateNew<ItemCrafting>();
        recipe.Number = recipeNumber;
        recipe.Name = $"Cherry Blossom {count} Branch Exchange";
        var settings = this.Context.CreateNew<SimpleCraftingSettings>();
        settings.SuccessPercent = 100;
        settings.ResultItemSelect = ResultItemSelection.Any;
        var input = this.Context.CreateNew<ItemCraftingRequiredItem>();
        input.PossibleItems.Add(this.Item(BranchGroup, branchNumber));
        input.MinimumAmount = input.MaximumAmount = count;
        input.SuccessResult = MixResult.Disappear;
        input.FailResult = MixResult.StaysAsIs;
        settings.RequiredItems.Add(input);
        foreach (var reward in rewards)
        {
            // Each entry is an equal lottery slot: Gold uses 9 slots per ordinary reward
            // and 5 for Golden Box, giving exactly 18/18/18/18/18/10 out of 50.
            var weight = branchNumber != 90 ? 1 : reward == ((byte)14, (short)123) ? GoldenBoxWeight : OrdinaryGoldWeight;
            for (var slot = 0; slot < weight; slot++)
            {
                this.AddResult(settings, reward, branchNumber == 90 ? (byte)10 : (byte)1);
            }
        }

        recipe.SimpleCraftingSettings = settings;
        spirit.ItemCraftings.Add(recipe);
    }

    private void AddResult(SimpleCraftingSettings settings, (byte Group, short Number) reward, byte ticketUses)
    {
        var result = this.Context.CreateNew<ItemCraftingResultItem>();
        result.ItemDefinition = this.Item(reward.Group, reward.Number);
        if (reward.Group == 13 && reward.Number is >= 46 and <= 48)
        {
            result.Durability = ticketUses;
        }

        settings.ResultItems.Add(result);
    }

    private ItemDefinition Item(byte group, short number) => this.GameConfiguration.Items.Single(item => item.Group == group && item.Number == number);
}
