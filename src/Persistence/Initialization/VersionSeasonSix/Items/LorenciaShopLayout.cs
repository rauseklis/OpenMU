// <copyright file="LorenciaShopLayout.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;

/// <summary>
/// Arranges Lumen's existing stock into horizontal progression rows without changing any item properties except its slot.
/// </summary>
public sealed class LorenciaShopLayout : InitializerBase
{
    private const int Width = 8;
    private const int Height = 15;

    /// <summary>
    /// Initializes a new instance of the <see cref="LorenciaShopLayout"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    public LorenciaShopLayout(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        var store = this.GameConfiguration.Monsters.Single(monster => monster.Number == 255).MerchantStore
            ?? throw new InvalidOperationException("Lumen has no merchant store.");
        var original = store.Items.OrderBy(item => item.ItemSlot).ToArray();
        var occupied = new bool[Width * Height];
        var placements = new Dictionary<Item, byte>();

        PlaceRow(0, [(14, 11, 5), (14, 11, 6), (14, 11, 3), (14, 11, 2), (14, 11, 1), (14, 32, 0), (14, 34, 0), (14, 33, 0)]);
        PlaceRow(1, [(14, 11, 7), (14, 11, 8), (14, 11, 9), (14, 11, 10), (14, 11, 11), (14, 11, 12)]);
        PlaceRow(2, [(12, 33, 0), (12, 34, 0), (12, 32, 0)]);
        PlaceRow(3, [(14, 11, 0), (14, 124, 0), (14, 123, 0)]);
        // Rows five and six stay entirely empty. Two-cell branches occupy rows seven/eight.
        PlaceRow(6, [(13, 47, 0), (13, 46, 0), (13, 48, 0), (14, 84, 0), (14, 88, 0), (14, 89, 0), (14, 90, 0)]);

        foreach (var item in original.Where(item => !placements.ContainsKey(item)))
        {
            var slot = Enumerable.Range(8 * Width, (Height - 8) * Width).FirstOrDefault(slot => Fits(item, slot), -1);
            if (slot < 0)
            {
                throw new InvalidOperationException("Lumen's remaining stock does not fit below the reserved rows; no slots were changed.");
            }

            Place(item, slot);
        }

        // Plan the entire grid first so a full/custom shop cannot be partially rearranged.
        foreach (var (item, slot) in placements)
        {
            item.ItemSlot = slot;
        }

        void PlaceRow(int row, (int Group, int Number, int Level)[] keys)
        {
            for (var column = 0; column < keys.Length; column++)
            {
                var key = keys[column];
                var item = original.FirstOrDefault(item => !placements.ContainsKey(item) && item.Definition is { } definition
                    && definition.Group == key.Group && definition.Number == key.Number && item.Level == key.Level);
                if (item is not null)
                {
                    Place(item, row * Width + column);
                }
            }
        }

        bool Fits(Item item, int slot)
        {
            if (item.Definition is not { } definition || definition.Width == 0 || definition.Height == 0
                || slot % Width + definition.Width > Width || slot / Width + definition.Height > Height)
            {
                return false;
            }

            for (var row = 0; row < definition.Height; row++)
            {
                for (var column = 0; column < definition.Width; column++)
                {
                    if (occupied[slot + row * Width + column])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        void Place(Item item, int slot)
        {
            if (!Fits(item, slot))
            {
                throw new InvalidOperationException("A reserved Lumen row cannot fit its item; no slots were changed.");
            }

            for (var row = 0; row < item.Definition!.Height; row++)
            {
                for (var column = 0; column < item.Definition.Width; column++)
                {
                    occupied[slot + row * Width + column] = true;
                }
            }

            placements.Add(item, (byte)slot);
        }
    }
}
