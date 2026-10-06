// <copyright file="AuctionNoteAndCategoryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.GameLogic.AuctionHouse;

/// <summary>
/// Verifies listing-note validation and the category classification of items.
/// </summary>
[TestFixture]
public class AuctionNoteAndCategoryTests
{
    /// <summary>
    /// Verifies a note is trimmed, and that an empty or whitespace note means no note.
    /// </summary>
    [Test]
    public void NoteIsTrimmedAndBlankMeansNoNote()
    {
        Assert.That(AuctionNote.Normalize("  hello  ").Value, Is.EqualTo("hello"));
        Assert.That(AuctionNote.Normalize("   ").Value, Is.Null);
        Assert.That(AuctionNote.Normalize(null).Value, Is.Null);
        Assert.That(AuctionNote.Normalize("   ").IsValid, Is.True);
    }

    /// <summary>
    /// Verifies control characters are rejected in a note.
    /// </summary>
    [Test]
    public void ControlCharactersAreRejected()
    {
        Assert.That(AuctionNote.Normalize("a\nb").IsValid, Is.False);
        Assert.That(AuctionNote.Normalize("a\tb").IsValid, Is.False);
    }

    /// <summary>
    /// Verifies the encoded-byte limit is eighty bytes, counted after trimming.
    /// </summary>
    [Test]
    public void NoteIsLimitedToEightyEncodedBytes()
    {
        Assert.That(AuctionNote.Normalize(new string('a', 80)).IsValid, Is.True);
        Assert.That(AuctionNote.Normalize(new string('a', 81)).IsValid, Is.False);
        Assert.That(AuctionNote.Normalize("   " + new string('a', 80) + "  ").IsValid, Is.True);
    }

    /// <summary>
    /// Verifies multibyte UTF-8 characters are counted by their encoded length.
    /// </summary>
    [Test]
    public void MultibyteCharactersCountByEncodedLength()
    {
        Assert.That(AuctionNote.Normalize(new string('é', 40)).IsValid, Is.True);
        Assert.That(AuctionNote.Normalize(new string('é', 41)).IsValid, Is.False);
    }

    /// <summary>
    /// Verifies a lone UTF-16 surrogate, which is not valid UTF-8, is rejected.
    /// </summary>
    [Test]
    public void LoneSurrogateIsRejected()
    {
        Assert.That(AuctionNote.Normalize("\uD800").IsValid, Is.False);
    }

    /// <summary>
    /// Verifies the default classification of item groups.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="category">The expected category.</param>
    [TestCase(0, 1, AuctionCategory.Weapon)]
    [TestCase(5, 1, AuctionCategory.Weapon)]
    [TestCase(6, 1, AuctionCategory.Armor)]
    [TestCase(11, 1, AuctionCategory.Armor)]
    [TestCase(12, 0, AuctionCategory.Wing)]
    [TestCase(13, 0, AuctionCategory.PetHelper)]
    [TestCase(14, 0, AuctionCategory.Consumable)]
    [TestCase(15, 0, AuctionCategory.Miscellaneous)]
    [TestCase(16, 0, AuctionCategory.Miscellaneous)]
    public void GroupsClassifyByDefaultRanges(int group, int number, AuctionCategory category)
    {
        Assert.That(AuctionCategoryClassifier.Classify(group, number), Is.EqualTo(category));
    }

    /// <summary>
    /// Verifies the canonical jewels are classified as material even though their groups are otherwise
    /// wings or consumables.
    /// </summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    [TestCase(12, 15)]
    [TestCase(14, 13)]
    [TestCase(14, 42)]
    public void CanonicalJewelsAreMaterialsEvenWithinOtherGroups(int group, int number)
    {
        Assert.That(AuctionCategoryClassifier.Classify(group, number), Is.EqualTo(AuctionCategory.JewelMaterial));
    }

    /// <summary>
    /// Verifies configured material overrides take precedence over their group's default category.
    /// </summary>
    [Test]
    public void ConfiguredMaterialOverrideTakesPrecedence()
    {
        var overrides = new[] { (Group: 14, Number: 99) };

        Assert.That(AuctionCategoryClassifier.Classify(14, 99, overrides), Is.EqualTo(AuctionCategory.JewelMaterial));
        Assert.That(AuctionCategoryClassifier.Classify(14, 0, overrides), Is.EqualTo(AuctionCategory.Consumable));
    }
}
