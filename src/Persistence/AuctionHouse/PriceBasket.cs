// <copyright file="PriceBasket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Globalization;

/// <summary>
/// A quantity for each of the 13 price kinds (Zen, the seven jewels, the five stat fruits), in <see cref="PriceKind"/> order.
/// A price, a bid, a refund or a set of proceeds of a mixed-currency listing is a basket. Zero means the kind is absent.
/// </summary>
public readonly struct PriceBasket : IEquatable<PriceBasket>
{
    /// <summary>
    /// The number of price kinds.
    /// </summary>
    public const int Size = 13;

    private readonly long[]? _quantities;

    private PriceBasket(long[] quantities)
    {
        this._quantities = quantities;
    }

    /// <summary>
    /// Gets the basket with no quantities.
    /// </summary>
    public static PriceBasket Empty => default;

    /// <summary>
    /// Gets a value indicating whether the basket has no quantity at all.
    /// </summary>
    public bool IsEmpty => this._quantities is null || this._quantities.All(q => q == 0);

    /// <summary>
    /// Gets the priced components, in kind order, without the empty ones.
    /// </summary>
    public IReadOnlyList<PriceComponent> Components
    {
        get
        {
            var components = new List<PriceComponent>();
            for (var kind = 0; kind < Size; kind++)
            {
                var quantity = this.Get((PriceKind)kind);
                if (quantity > 0)
                {
                    components.Add(new PriceComponent((PriceKind)kind, quantity));
                }
            }

            return components;
        }
    }

    /// <summary>
    /// Gets a bit mask with one bit (the kind's number) for every priced kind.
    /// </summary>
    public int KindMask
    {
        get
        {
            var mask = 0;
            for (var kind = 0; kind < Size; kind++)
            {
                if (this.Get((PriceKind)kind) > 0)
                {
                    mask |= 1 << kind;
                }
            }

            return mask;
        }
    }

    /// <summary>
    /// Builds a basket from components. Each kind may appear once.
    /// </summary>
    /// <param name="components">The components.</param>
    /// <returns>The basket.</returns>
    public static PriceBasket FromComponents(IEnumerable<PriceComponent> components)
    {
        var quantities = new long[Size];
        foreach (var component in components)
        {
            var index = (int)component.Kind;
            if (index < 0 || index >= Size)
            {
                throw new ArgumentException($"{component.Kind} is not a price kind.", nameof(components));
            }

            if (quantities[index] != 0)
            {
                throw new ArgumentException($"{component.Kind} appears twice.", nameof(components));
            }

            quantities[index] = component.Quantity;
        }

        return new PriceBasket(quantities);
    }

    /// <summary>
    /// Reads a basket from its stored text, thirteen comma-separated quantities.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <returns>The basket, or <c>null</c> when the text is missing or malformed.</returns>
    public static PriceBasket? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Split(',');
        if (parts.Length != Size)
        {
            return null;
        }

        var quantities = new long[Size];
        for (var i = 0; i < Size; i++)
        {
            if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out quantities[i]))
            {
                return null;
            }
        }

        return new PriceBasket(quantities);
    }

    /// <summary>
    /// Gets the quantity of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The quantity, zero when absent.</returns>
    public long Get(PriceKind kind) => this._quantities is null ? 0 : this._quantities[(int)kind];

    /// <summary>
    /// Writes the stored text form.
    /// </summary>
    /// <returns>Thirteen comma-separated quantities.</returns>
    public string ToVectorText()
    {
        var copy = this;
        return string.Join(',', Enumerable.Range(0, Size).Select(i => copy.Get((PriceKind)i).ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Gets a value indicating whether every kind has at least the quantity <paramref name="other"/> has.
    /// </summary>
    /// <param name="other">The basket to compare with.</param>
    /// <returns><c>true</c> when no kind is lower.</returns>
    public bool Dominates(PriceBasket other)
    {
        for (var kind = 0; kind < Size; kind++)
        {
            if (this.Get((PriceKind)kind) < other.Get((PriceKind)kind))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a value indicating whether this basket is a raise of <paramref name="current"/>: no kind lower and at least one higher.
    /// </summary>
    /// <param name="current">The basket to raise.</param>
    /// <returns><c>true</c> when it is a raise.</returns>
    public bool IsRaiseOf(PriceBasket current) => this.Dominates(current) && !this.Equals(current);

    /// <summary>
    /// Subtracts another basket kind by kind, never going below zero.
    /// </summary>
    /// <param name="other">The basket to subtract.</param>
    /// <returns>The remainder.</returns>
    public PriceBasket Minus(PriceBasket other)
    {
        var quantities = new long[Size];
        for (var kind = 0; kind < Size; kind++)
        {
            quantities[kind] = Math.Max(0, this.Get((PriceKind)kind) - other.Get((PriceKind)kind));
        }

        return new PriceBasket(quantities);
    }

    /// <inheritdoc />
    public bool Equals(PriceBasket other)
    {
        for (var kind = 0; kind < Size; kind++)
        {
            if (this.Get((PriceKind)kind) != other.Get((PriceKind)kind))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PriceBasket other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        for (var kind = 0; kind < Size; kind++)
        {
            hash.Add(this.Get((PriceKind)kind));
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => this.ToVectorText();
}
