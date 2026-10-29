// <copyright file="PetDurabilityPolicy.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Pet;

using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.World;

/// <summary>Shared x5/x50 pet wear policy. Calibration is damage/hits, never elapsed time.</summary>
public static class PetDurabilityPolicy
{
    /// <summary>Raven selected-hit divisor: 3.28 callbacks/s, selection probability 1/3, 72 hours, 255 Life.</summary>
    public const double RavenHitsPerLife = 3.28 * 3600 * 72 / (3 * 255);

    /// <summary>Whether this is one of the supported Season Six pets, independent of equipped slot.</summary>
    public static bool IsPet(Item item) => item.Definition is { Group: 13, Number: 0 or 1 or 2 or 3 or 4 or 5 or 37 or 64 or 65 or 67 or 80 or 106 or 123 };

    /// <summary>Whether the pet survives exhaustion as a repairable item.</summary>
    public static bool RetainAtZero(Item item) => item.Definition is { Group: 13, Number: 4 or 5 or 37 };

    /// <summary>Whether this pet can provide benefits or attack right now.</summary>
    public static bool IsUsable(Item item) => item.Durability > 0 && !item.IsRentalExpired();

    /// <summary>Checks mount skills both at casting time and when delayed damage/effects resolve.</summary>
    public static bool CanUseMountSkill(Player player, short skillNumber)
    {
        var number = skillNumber switch { 62 => 4, 76 => 37, _ => -1 };
        return number < 0 || (player.Inventory?.GetItem(InventoryConstants.PetSlot) is { } mount
            && mount.Definition is { Group: 13 } && mount.Definition.Number == number && IsUsable(mount));
    }

    /// <summary>The approved normal-farming lifespan; Raven has a separate hit-based calibration.</summary>
    public static int TargetHours(Item item) => item.Definition?.Number switch
    {
        0 or 1 or 2 or 106 => 3,
        3 or 64 or 65 or 67 or 80 or 123 => 6,
        37 => 24,
        4 or 5 => 72,
        _ => throw new ArgumentException("Unsupported pet.", nameof(item)),
    };

    /// <summary>Damage divisor from 50 counted HP/s and 255 full Life.</summary>
    public static double DamageDivisor(Item item) => 50.0 * 3600 * TargetHours(item) / 255;

    /// <summary>Damage wear. A persisted rental timestamp distinguishes Cash Shop items from ordinary drops.</summary>
    public static double DamageLoss(Item item, double countedDamage, double protectionPercent = 0)
        => item.IsDarkRaven() || item.RentalExpiresAt.HasValue || !IsUsable(item) || countedDamage <= 0
            ? 0 : countedDamage / DamageDivisor(item) * ProtectionMultiplier(protectionPercent);

    /// <summary>Hit wear is charged only after the existing random selector chooses Raven.</summary>
    public static double RavenLoss(Item item, double protectionPercent = 0)
        => !item.IsDarkRaven() || item.RentalExpiresAt.HasValue || !IsUsable(item) ? 0 : ProtectionMultiplier(protectionPercent) / RavenHitsPerLife;

    /// <summary>Converts a displayed percent to a nonnegative remaining wear fraction.</summary>
    public static double ProtectionMultiplier(double percent) => double.IsFinite(percent) ? 1 - Math.Clamp(percent, 0, 100) / 100 : 1;

    /// <summary>Applies fractional wear and handles zero transitions independently of integer packet rounding.</summary>
    public static async ValueTask ApplyLossAsync(Player player, Item item, double loss)
    {
        if (item.RentalExpiresAt.HasValue || loss <= 0 || !double.IsFinite(loss) || !IsUsable(item))
        {
            return;
        }

        var changed = item.DecreaseDurability(loss);
        var exhausted = item.Durability <= 0;
        if (changed || exhausted)
        {
            await player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
        }

        if (!exhausted)
        {
            return;
        }

        if (!RetainAtZero(item))
        {
            await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
            return;
        }

        // Level, experience, options and color are deliberately untouched.
        await RefreshAsync(player, item).ConfigureAwait(false);
        if (item.Definition?.Number == 5)
        {
            await player.ResetPetBehaviorAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Rebuilds equipped pet attributes/appearance after exhaustion or a successful repair.</summary>
    public static async ValueTask RefreshAsync(Player player, Item item)
    {
        if (player.Inventory is InventoryStorage inventory && inventory.EquippedItems.Contains(item))
        {
            await inventory.RefreshPetAsync(item).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IUpdateInventoryListPlugIn>(p => p.UpdateInventoryListAsync()).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IAppearanceChangedPlugIn>(p => p.AppearanceChangedAsync(player, item, true)).ConfigureAwait(false);
        }
    }
}
