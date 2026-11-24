// <copyright file="ResetFruitTiersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the fruit costs of the resets to fixed amounts per reset range (Jewel Bank fruits):
/// 25 from reset 6, 50 from reset 11, 75 from reset 21, 100 from reset 31 and 150 from reset 41.
/// Only the item cost tiers of the reset configuration are replaced; every other setting stays as it is.
/// </summary>
[PlugIn]
[Guid("A3D7C1E5-4B28-4F96-8E0A-1C5F7B9D2E64")]
public sealed class ResetFruitTiersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The fruit costs: the first reset of the range and the fruits for each reset in it.
    /// </summary>
    internal static readonly (int MinimumResetCount, int Fruits)[] Tiers =
    [
        (6, 25),
        (11, 50),
        (21, 75),
        (31, 100),
        (41, 150),
    ];

    /// <inheritdoc />
    public override string Name => "Reset fruit costs";

    /// <inheritdoc />
    public override string Description => "The fruit costs of the resets are fixed per reset range: 25 (from reset 6), 50 (11), 75 (21), 100 (31) and 150 (41).";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ResetFruitTiers;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 24, 17, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Replaces the item cost tiers in the stored JSON of the reset configuration.
    /// </summary>
    /// <param name="json">The stored configuration.</param>
    /// <returns>The changed configuration, or <c>null</c> if the configuration has an unexpected shape and was not changed.</returns>
    public static string? ReplaceItemCostTiers(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root
            || root["ItemCostTiers"] is not JsonObject tiers
            || tiers["$values"] is not JsonArray)
        {
            return null;
        }

        var values = new JsonArray();
        foreach (var (minimumResetCount, fruits) in Tiers)
        {
            values.Add(new JsonObject
            {
                ["$id"] = string.Empty,
                [nameof(ResetConfiguration.ResetItemCostTier.MinimumResetCount)] = minimumResetCount,
                [nameof(ResetConfiguration.ResetItemCostTier.RequiredItemAmount)] = fruits,
                [nameof(ResetConfiguration.ResetItemCostTier.AdditionalItemAmountPerReset)] = 0,
            });
        }

        tiers["$values"] = values;
        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var typeId = typeof(ResetFeaturePlugIn).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration?.CustomConfiguration is { Length: > 0 } json
            && ReplaceItemCostTiers(json) is { } changed)
        {
            plugInConfiguration.CustomConfiguration = changed;
        }

        return ValueTask.CompletedTask;
    }
}
