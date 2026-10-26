// <copyright file="BonusProgression.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Installs reviewed opening pools and exact monster assignments without changing shops or events.
/// </summary>
public sealed class BonusProgression : InitializerBase
{
    private const string ResourceName = "MUnique.OpenMU.Persistence.Initialization.Resources.BonusProgression.json";
    private const byte WrapperGroup = 14;
    private const short WrapperNumber = 11;

    /// <summary>
    /// Initializes a new instance of the <see cref="BonusProgression"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected game configuration.</param>
    public BonusProgression(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        using var stream = typeof(BonusProgression).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The reviewed bonus progression resource is missing.");
        using var plan = JsonDocument.Parse(stream);
        var wrapper = this.GameConfiguration.Items.Single(item => item.Group == WrapperGroup && item.Number == WrapperNumber);
        this.ConfigureOpeningRewards(wrapper, plan.RootElement.GetProperty("rewards"));
        this.ConfigureMonsterBonuses(wrapper, plan.RootElement);
    }

    private static double? OptionalChance(JsonElement reward, string name) => reward.GetProperty(name).ValueKind == JsonValueKind.Null
        ? null : reward.GetProperty(name).GetDouble();

    private void ConfigureOpeningRewards(ItemDefinition wrapper, JsonElement rewards)
    {
        var levels = rewards.EnumerateArray().Select(reward => reward.GetProperty("level").GetByte()).ToHashSet();
        foreach (var old in wrapper.DropItems.Where(group => levels.Contains(group.SourceItemLevel)).ToArray())
        {
            wrapper.DropItems.Remove(old);
        }

        foreach (var reward in rewards.EnumerateArray())
        {
            var group = this.Context.CreateNew<ItemDropItemGroup>();
            group.Description = reward.GetProperty("description").GetString()!;
            group.SourceItemLevel = reward.GetProperty("level").GetByte();
            group.Chance = reward.GetProperty("chance").GetDouble();
            group.ItemType = (SpecialItemType)reward.GetProperty("type").GetInt32();
            group.MinimumLevel = reward.GetProperty("minimum").GetByte();
            group.MaximumLevel = reward.GetProperty("maximum").GetByte();
            group.MoneyAmount = reward.GetProperty("money").GetInt32();
            group.DropEffect = (ItemDropEffect)reward.GetProperty("effect").GetInt32();
            group.ExcellentSecondOptionChance = OptionalChance(reward, "second");
            group.SkillChance = OptionalChance(reward, "skill");
            group.LuckChance = OptionalChance(reward, "luck");
            group.GuaranteeJewelryOption = reward.GetProperty("jewelry").GetBoolean();
            group.SuppressAdditionalOption = reward.GetProperty("suppressAdd").GetBoolean();
            foreach (var key in reward.GetProperty("items").EnumerateArray())
            {
                group.PossibleItems.Add(this.FindItem(key));
            }

            wrapper.DropItems.Add(group);
        }
    }

    private void ConfigureMonsterBonuses(ItemDefinition wrapper, JsonElement plan)
    {
        var eventGroups = this.GameConfiguration.MiniGameDefinitions.SelectMany(game => game.Rewards)
            .Where(reward => reward.ItemReward is not null).Select(reward => reward.ItemReward!).ToHashSet();
        foreach (var old in this.GameConfiguration.DropItemGroups.Where(group => group.ItemType == SpecialItemType.SmallBonus && !eventGroups.Contains(group)).ToArray())
        {
            foreach (var map in this.GameConfiguration.Maps)
            {
                map.DropItemGroups.Remove(old);
            }

            foreach (var monster in this.GameConfiguration.Monsters)
            {
                monster.DropItemGroups.Remove(old);
            }

            this.GameConfiguration.DropItemGroups.Remove(old);
        }

        this.RemoveOldOrdinaryBonusSources(wrapper, plan, eventGroups);
        foreach (var bonus in plan.GetProperty("bonuses").EnumerateArray())
        {
            var group = this.Context.CreateNew<DropItemGroup>();
            group.Description = $"Approved bonus: {bonus.GetProperty("bonus").GetString()} / {bonus.GetProperty("name").GetString()}";
            group.ItemType = SpecialItemType.SmallBonus;
            group.Chance = 1;
            group.ItemLevel = bonus.GetProperty("level").GetByte();
            group.Monster = this.GameConfiguration.Monsters.Single(monster => monster.Number == bonus.GetProperty("monster").GetInt16());
            group.PossibleItems.Add(wrapper);
            this.GameConfiguration.DropItemGroups.Add(group);
            this.GameConfiguration.Maps.Single(map => map.Number == bonus.GetProperty("map").GetInt16()).DropItemGroups.Add(group);
        }
    }

    private void RemoveOldOrdinaryBonusSources(ItemDefinition wrapper, JsonElement plan, HashSet<DropItemGroup> eventGroups)
    {
        var excludedKeys = plan.GetProperty("excludedItems").EnumerateArray()
            .Select(key => (Group: key[0].GetByte(), Number: key[1].GetInt16())).ToHashSet();
        var excluded = this.GameConfiguration.Items.Where(item => excludedKeys.Contains((item.Group, item.Number))).ToHashSet();
        var levels = plan.GetProperty("excludedWrapperLevels").EnumerateArray().Select(level => level.GetByte()).ToHashSet();
        foreach (var item in excluded.Append(wrapper))
        {
            item.DropsFromMonsters = false;
        }

        var ordinaryGroups = this.GameConfiguration.Maps.SelectMany(map => map.DropItemGroups)
            .Concat(this.GameConfiguration.Monsters.SelectMany(monster => monster.DropItemGroups)).ToHashSet();
        foreach (var group in ordinaryGroups.Where(group => group.ItemType != SpecialItemType.SmallBonus && !eventGroups.Contains(group)))
        {
            var removed = group.PossibleItems.Where(item => excluded.Contains(item)
                || (item == wrapper && levels.Contains(group.ItemLevel ?? 0))).ToArray();
            foreach (var item in removed)
            {
                group.PossibleItems.Remove(item);
            }

            if (removed.Length > 0 && group.PossibleItems.Count == 0)
            {
                foreach (var map in this.GameConfiguration.Maps)
                {
                    map.DropItemGroups.Remove(group);
                }

                foreach (var monster in this.GameConfiguration.Monsters)
                {
                    monster.DropItemGroups.Remove(group);
                }
            }
        }
    }

    private ItemDefinition FindItem(JsonElement key) => this.GameConfiguration.Items.Single(
        item => item.Group == key[0].GetByte() && item.Number == key[1].GetInt16());

}
