namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Boosts ordinary chance-based item drops without duplicating guaranteed or quest rewards.
/// </summary>
public static class PremiumDropChance
{
    public static double Weight(DropItemGroup group, float bonus) => group.Chance < 1
        && group.ItemType is SpecialItemType.RandomItem or SpecialItemType.Excellent or SpecialItemType.Ancient
        ? group.Chance * (1 + Math.Max(0, bonus))
        : group.Chance;
}
