// <copyright file="ExcellentJewellery.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Identifies the excellent rings and pendants (Ring of Fire, Pendant of Ice, ...), which must not be dropped
/// as excellent items by monsters, events or boxes. The items themselves still exist and can be obtained otherwise.
/// </summary>
public static class ExcellentJewellery
{
    private const byte JewelleryGroup = 13;

    /// <summary>
    /// Gets a value indicating whether the item definition is a ring or pendant which can have excellent options.
    /// </summary>
    /// <param name="definition">The item definition.</param>
    /// <returns><c>true</c>, if it's an excellent-capable ring or pendant.</returns>
    public static bool IsExcellentRingOrPendant(ItemDefinition definition)
    {
        return definition.Group == JewelleryGroup
            && definition.PossibleItemOptions.Any(
                option => option.PossibleOptions.Any(possible => object.Equals(possible.OptionType, ItemOptionTypes.Excellent)));
    }
}
