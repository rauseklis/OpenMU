// <copyright file="AddLostMapsToLumenStorePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds Lost Map levels 1 through 7 to Lumen the Barmaid's merchant store.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("76d43b0e-3395-4dc6-9a9d-7944520d1253")]
public class AddLostMapsToLumenStorePlugIn : UpdatePlugInBase
{
    /// <summary>
    /// The plug-in name.
    /// </summary>
    internal const string PlugInName = "Add Lost Maps to Lumen's store";

    /// <summary>
    /// The plug-in description.
    /// </summary>
    internal const string PlugInDescription = "Adds Lost Map levels 1 through 7 to Lumen the Barmaid's Lorencia store.";

    private const short LumenNpcNumber = 255;
    private const byte LostMapGroup = 14;
    private const byte LostMapNumber = 28;
    private const byte FirstStoreSlot = 72;
    private const byte FirstLostMapLevel = 1;
    private const byte LastLostMapLevel = 7;

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddLostMapsToLumenStore;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 09, 29, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var store = gameConfiguration.Monsters.FirstOrDefault(monster => monster.Number == LumenNpcNumber)?.MerchantStore;
        if (store is null)
        {
            return default;
        }

        var itemHelper = new ItemHelper(context, gameConfiguration);
        for (byte level = FirstLostMapLevel; level <= LastLostMapLevel; level++)
        {
            if (store.Items.Any(item => item.Definition is { Group: LostMapGroup, Number: LostMapNumber } && item.Level == level))
            {
                continue;
            }

            var slot = (byte)(FirstStoreSlot + level - FirstLostMapLevel);
            store.Items.Add(itemHelper.CreateItem(slot, LostMapNumber, LostMapGroup, 1, level));
        }

        return default;
    }
}
