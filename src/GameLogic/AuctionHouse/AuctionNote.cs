// <copyright file="AuctionNote.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Text;

/// <summary>
/// The outcome of validating a listing note.
/// </summary>
/// <param name="IsValid">Whether the note is acceptable.</param>
/// <param name="Value">The trimmed note, or <c>null</c> when there is no note.</param>
public readonly record struct AuctionNoteResult(bool IsValid, string? Value);

/// <summary>
/// Validates a listing note: strict UTF-8, trimmed, no control characters, and at most 80 encoded bytes.
/// </summary>
public static class AuctionNote
{
    /// <summary>
    /// The maximum length of a note in UTF-8 bytes, after trimming.
    /// </summary>
    public const int MaximumEncodedBytes = 80;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Validates and normalizes a note.
    /// </summary>
    /// <param name="note">The note as entered.</param>
    /// <returns>The validation result. A blank note is valid and means no note.</returns>
    public static AuctionNoteResult Normalize(string? note)
    {
        if (note is null)
        {
            return new AuctionNoteResult(true, null);
        }

        var trimmed = note.Trim();
        if (trimmed.Length == 0)
        {
            return new AuctionNoteResult(true, null);
        }

        if (trimmed.Any(char.IsControl))
        {
            return new AuctionNoteResult(false, null);
        }

        byte[] encoded;
        try
        {
            encoded = StrictUtf8.GetBytes(trimmed);
        }
        catch (EncoderFallbackException)
        {
            return new AuctionNoteResult(false, null);
        }

        return encoded.Length > MaximumEncodedBytes
            ? new AuctionNoteResult(false, null)
            : new AuctionNoteResult(true, trimmed);
    }
}
