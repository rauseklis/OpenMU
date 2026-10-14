// <copyright file="PriceBasketWire.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Buffers.Binary;

/// <summary>
/// The wire form of a <see cref="PriceBasket"/>: thirteen little-endian 32-bit quantities in <see cref="PriceKind"/> order.
/// </summary>
public static class PriceBasketWire
{
    /// <summary>
    /// The size of a basket on the wire, in bytes.
    /// </summary>
    public const int Size = PriceBasket.Size * sizeof(uint);

    /// <summary>
    /// Writes a basket. A quantity above the 32-bit range is clamped, which no configured limit allows.
    /// </summary>
    /// <param name="destination">The destination, at least <see cref="Size"/> bytes.</param>
    /// <param name="basket">The basket.</param>
    public static void Write(Span<byte> destination, PriceBasket basket)
    {
        for (var kind = 0; kind < PriceBasket.Size; kind++)
        {
            var quantity = (uint)Math.Min(basket.Get((PriceKind)kind), uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(kind * sizeof(uint), sizeof(uint)), quantity);
        }
    }

    /// <summary>
    /// Writes a basket from its stored text; text which is missing or malformed is written as an empty basket.
    /// </summary>
    /// <param name="destination">The destination, at least <see cref="Size"/> bytes.</param>
    /// <param name="vectorText">The stored text, or <c>null</c>.</param>
    public static void Write(Span<byte> destination, string? vectorText)
        => Write(destination, PriceBasket.Parse(vectorText) ?? PriceBasket.Empty);

    /// <summary>
    /// Reads a basket. A source which is too short reads as an empty basket.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The basket.</returns>
    public static PriceBasket Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size)
        {
            return PriceBasket.Empty;
        }

        var components = new List<PriceComponent>();
        for (var kind = 0; kind < PriceBasket.Size; kind++)
        {
            var quantity = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(kind * sizeof(uint), sizeof(uint)));
            if (quantity > 0)
            {
                components.Add(new PriceComponent((PriceKind)kind, quantity));
            }
        }

        return PriceBasket.FromComponents(components);
    }
}
