// <copyright file="KundunProgression.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Replaces only Kundun +1 through +5 with class-first pre-Pink Excellent rewards.
/// </summary>
public sealed class KundunProgression : InitializerBase
{
    private const string ResourceName = "MUnique.OpenMU.Persistence.Initialization.Resources.KundunProgression.json";
    private const int WrapperLevelOffset = 7;

    /// <summary>
    /// Initializes a new instance of the <see cref="KundunProgression"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public KundunProgression(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        using var stream = typeof(KundunProgression).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The reviewed Kundun progression resource is missing.");
        using var plan = JsonDocument.Parse(stream);
        var wrapper = this.GameConfiguration.Items.Single(item => item.Group == 14 && item.Number == 11);
        foreach (var old in wrapper.DropItems.Where(group => group.SourceItemLevel is >= 8 and <= 12).ToArray())
        {
            wrapper.DropItems.Remove(old);
        }

        foreach (var reward in plan.RootElement.EnumerateArray())
        {
            this.AddGroup(wrapper, reward);
        }
    }

    private void AddGroup(ItemDefinition wrapper, JsonElement reward)
    {
        var tier = reward.GetProperty("tier").GetByte();
        var group = this.Context.CreateNew<ItemDropItemGroup>();
        group.Description = $"Kundun +{tier} / {reward.GetProperty("category").GetString()} / {reward.GetProperty("class").GetString()} / {reward.GetProperty("variant").GetInt32()}";
        group.SourceItemLevel = (byte)(tier + WrapperLevelOffset);
        group.Chance = reward.GetProperty("chance").GetDouble();
        group.ItemType = SpecialItemType.Excellent;
        group.MinimumLevel = 0;
        group.MaximumLevel = 0;
        group.ExcellentSecondOptionChance = (6 - tier) / 10.0;
        group.DropEffect = ItemDropEffect.Fireworks;
        foreach (var key in reward.GetProperty("items").EnumerateArray())
        {
            var item = this.GameConfiguration.Items.Single(item => item.Group == key[0].GetByte() && item.Number == key[1].GetInt16());
            var family = reward.GetProperty("family").GetInt32();
            if (!item.QualifiedCharacters.Any(character => character.Number / 4 == family))
            {
                throw new InvalidOperationException($"Kundun item {item.Group}/{item.Number} is not qualified for the reviewed class.");
            }

            group.PossibleItems.Add(item);
        }

        wrapper.DropItems.Add(group);
    }
}
