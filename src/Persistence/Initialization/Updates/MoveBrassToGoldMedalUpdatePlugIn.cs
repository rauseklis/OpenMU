// <copyright file="MoveBrassToGoldMedalUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>Moves the Brass armor set from the Silver Medal to the Gold Medal rewards and adds the Bronze set to the Silver Medal.</summary>
[PlugIn]
[Guid("F528A246-D1EC-4F99-9CEF-21C576D33512")]
public sealed class MoveBrassToGoldMedalUpdatePlugIn : UpdatePlugInBase
{
    private const byte WrapperGroup = 14;
    private const short WrapperNumber = 11;
    private const byte SilverLevel = 5;
    private const byte GoldLevel = 6;
    private const short BronzeSet = 0;
    private const short BrassSet = 8;

    /// <inheritdoc />
    public override string Name => "Brass to Gold Medal, Bronze to Silver Medal";

    /// <inheritdoc />
    public override string Description => "Moves the Brass armor set from the Silver Medal to the Gold Medal rewards and adds the Bronze set to the Silver Medal.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.MoveBrassToGoldMedal;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 22, 3, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var wrapper = gameConfiguration.Items.Single(item => item.Group == WrapperGroup && item.Number == WrapperNumber);
        var weights = new UniqueRewardWeights(context, gameConfiguration);

        var silverBrass = ArmorGroups(wrapper, SilverLevel).FirstOrDefault(group => group.PossibleItems.Any(item => item.Number == BrassSet));
        var goldArmor = ArmorGroups(wrapper, GoldLevel).ToList();
        if (silverBrass is null || goldArmor.Count == 0 || goldArmor.Any(group => group.PossibleItems.Any(item => item.Number == BrassSet)))
        {
            return ValueTask.CompletedTask;
        }

        var bronze = ArmorItems(gameConfiguration, BronzeSet);
        var brass = ArmorItems(gameConfiguration, BrassSet);
        if (bronze.Length == 0 || brass.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var goldTotal = goldArmor.Sum(group => group.Chance);
        var newGold = weights.CreateGroup(goldArmor[0], "armor", brass, 0);
        var newSilver = weights.CreateGroup(silverBrass, "armor", bronze, silverBrass.Chance);
        wrapper.DropItems.Remove(silverBrass);
        wrapper.DropItems.Add(newSilver);
        wrapper.DropItems.Add(newGold);
        goldArmor.Add(newGold);
        foreach (var group in goldArmor)
        {
            group.Chance = goldTotal / goldArmor.Count;
        }

        goldArmor[^1].Chance = goldTotal - goldArmor.Take(goldArmor.Count - 1).Sum(group => group.Chance);
        return ValueTask.CompletedTask;
    }

    private static ItemDefinition[] ArmorItems(GameConfiguration configuration, short set)
        => configuration.Items.Where(item => item.Group is >= 7 and <= 11 && item.Number == set).ToArray();

    private static IEnumerable<ItemDropItemGroup> ArmorGroups(ItemDefinition wrapper, byte level)
        => wrapper.DropItems.Where(group => group.SourceItemLevel == level && group.PossibleItems.Count > 0
            && group.PossibleItems.All(item => item.Group is >= 7 and <= 11));
}
