namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// The duration which was bought together with a seal or scroll in the shop.
/// It is kept in the otherwise unused <see cref="Item.PetExperience"/> of such an item (a plain saved number, so no database change is needed)
/// and is applied when the item is used. Items without it (older items) use the default duration of their effect.
/// </summary>
public static class PremiumItemDuration
{
    /// <summary>Gets the bought duration of the item, or null when none was stored.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The duration, or null.</returns>
    public static TimeSpan? Get(Item item)
        => item.PetExperience > 0 ? TimeSpan.FromSeconds(item.PetExperience) : null;

    /// <summary>Stores the bought duration on the item.</summary>
    /// <param name="item">The item.</param>
    /// <param name="seconds">The duration in seconds.</param>
    public static void Set(Item item, int seconds) => item.PetExperience = seconds;
}
