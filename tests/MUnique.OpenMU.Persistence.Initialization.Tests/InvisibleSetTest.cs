// <copyright file="InvisibleSetTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the Invisible set definitions and their installation at the start of the server.
/// </summary>
[TestFixture]
internal class InvisibleSetTest
{
    private static readonly byte[] Groups = { 7, 8, 9, 10, 11 };

    /// <summary>
    /// Tests that a new database has the five pieces with fixed defense, fixed stat requirements and no options.
    /// </summary>
    [Test]
    public async Task NewDatabaseHasTheFivePiecesAsync()
    {
        var configuration = await CreateConfigurationAsync().ConfigureAwait(false);
        var pieces = GetPieces(configuration);

        Assert.That(pieces.Select(p => p.Group), Is.EquivalentTo(Groups));
        Assert.That(pieces.Select(p => p.Name.ValueInNeutralLanguage), Is.EquivalentTo(new[] { "Invisible Helm", "Invisible Armor", "Invisible Pants", "Invisible Gloves", "Invisible Boots" }));
        foreach (var piece in pieces)
        {
            Assert.That(piece.DropsFromMonsters, Is.False, piece.Name.ValueInNeutralLanguage);
            Assert.That(piece.MaximumItemLevel, Is.EqualTo(15), piece.Name.ValueInNeutralLanguage);
            Assert.That(piece.PossibleItemOptions, Is.Empty, piece.Name.ValueInNeutralLanguage);
            Assert.That(piece.PossibleItemSetGroups, Is.Empty, piece.Name.ValueInNeutralLanguage);
            Assert.That(piece.ItemSlot, Is.Not.Null, piece.Name.ValueInNeutralLanguage);

            var defense = piece.BasePowerUpAttributes.Single(a => a.TargetAttribute == Stats.DefenseBase);
            Assert.That(defense.BaseValue, Is.EqualTo(InvisibleArmors.Defense), piece.Name.ValueInNeutralLanguage);
            Assert.That(defense.BonusPerLevelTable, Is.Null, piece.Name.ValueInNeutralLanguage);
        }
    }

    /// <summary>
    /// Tests that the defense and the stat requirements are the same at every item level.
    /// </summary>
    /// <param name="level">The item level.</param>
    [TestCase(0)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(9)]
    [TestCase(15)]
    public async Task RequirementsStayFixedAsync(int level)
    {
        var configuration = await CreateConfigurationAsync().ConfigureAwait(false);
        foreach (var piece in GetPieces(configuration))
        {
            var item = new TemporaryItem { Definition = piece, Level = (byte)level, Durability = piece.Durability };
            var requirements = piece.Requirements.Select(item.GetRequirement).ToDictionary(r => r.Attr, r => r.Value);
            Assert.That(requirements, Has.Count.EqualTo(4), piece.Name.ValueInNeutralLanguage);
            Assert.That(requirements[Stats.TotalStrength], Is.EqualTo(InvisibleArmors.StatRequirement), piece.Name.ValueInNeutralLanguage);
            Assert.That(requirements[Stats.TotalAgility], Is.EqualTo(InvisibleArmors.StatRequirement), piece.Name.ValueInNeutralLanguage);
            Assert.That(requirements[Stats.TotalVitality], Is.EqualTo(InvisibleArmors.StatRequirement), piece.Name.ValueInNeutralLanguage);
            Assert.That(requirements[Stats.TotalEnergy], Is.EqualTo(InvisibleArmors.StatRequirement), piece.Name.ValueInNeutralLanguage);
        }
    }

    /// <summary>
    /// Tests that only the gloves add attack speed, and by 100.
    /// </summary>
    [Test]
    public async Task OnlyTheGlovesAddAttackSpeedAsync()
    {
        var configuration = await CreateConfigurationAsync().ConfigureAwait(false);
        foreach (var piece in GetPieces(configuration))
        {
            var attackSpeed = piece.BasePowerUpAttributes.Where(a => a.TargetAttribute == Stats.AttackSpeedAny).ToList();
            if (piece.Group == 10)
            {
                Assert.That(attackSpeed.Single().BaseValue, Is.EqualTo(InvisibleArmors.GlovesAttackSpeed));
            }
            else
            {
                Assert.That(attackSpeed, Is.Empty, piece.Name.ValueInNeutralLanguage);
            }
        }
    }

    /// <summary>
    /// Tests that every class can wear every piece, except the Magic Gladiator the helm and the Rage Fighter the gloves.
    /// </summary>
    [Test]
    public async Task EveryClassExceptTheUsualExceptionsAsync()
    {
        var configuration = await CreateConfigurationAsync().ConfigureAwait(false);
        var allClasses = configuration.CharacterClasses.ToList();
        foreach (var piece in GetPieces(configuration))
        {
            var excluded = allClasses.Except(piece.QualifiedCharacters).Select(c => c.Number).ToList();
            var expected = piece.Group switch
            {
                7 => new[] { (byte)CharacterClassNumber.MagicGladiator, (byte)CharacterClassNumber.DuelMaster },
                10 => new[] { (byte)CharacterClassNumber.RageFighter, (byte)CharacterClassNumber.FistMaster },
                _ => Array.Empty<byte>(),
            };
            Assert.That(excluded, Is.EquivalentTo(expected), piece.Name.ValueInNeutralLanguage);
        }
    }

    /// <summary>
    /// Tests that the update of a database without the set is installed once and is recorded as installed.
    /// </summary>
    [Test]
    public async Task InstallsTheUpdateOnlyOnceAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            foreach (var piece in GetPieces(configuration))
            {
                configuration.Items.Remove(piece);
                await context.DeleteAsync(piece).ConfigureAwait(false);
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddInvisibleSet).ToList())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await InvisibleSetInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True, "first start");
        Assert.That(await InvisibleSetInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False, "second start");

        using var reloaded = provider.CreateNewContext();
        var reloadedConfiguration = (await reloaded.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        Assert.That(GetPieces(reloadedConfiguration), Has.Count.EqualTo(5));
        var installed = (await reloaded.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(u => u.Version == (int)UpdateVersion.AddInvisibleSet).ToList();
        Assert.That(installed, Has.Count.EqualTo(1));
        Assert.That(installed[0].InstalledAt, Is.Not.Null);
    }

    /// <summary>
    /// Tests that nothing is installed again when the update is installed already, e.g. in a new database.
    /// </summary>
    [Test]
    public async Task DoesNothingWhenTheUpdateIsInstalledAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);

        Assert.That(await InvisibleSetInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
    }

    private static async Task<GameConfiguration> CreateConfigurationAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory()).CreateInitialDataAsync(1, true).ConfigureAwait(false);
        using var context = provider.CreateNewContext();
        return (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
    }

    private static List<ItemDefinition> GetPieces(GameConfiguration configuration)
    {
        return configuration.Items.Where(i => i.Number == InvisibleArmors.Number && Groups.Contains(i.Group)).ToList();
    }
}
