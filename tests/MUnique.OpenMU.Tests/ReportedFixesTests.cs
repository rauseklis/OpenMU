// <copyright file="ReportedFixesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using SeasonSixDataInitialization = MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.DataInitialization;

/// <summary>
/// Verifies the stat commands, the Scroll of Flame radius and the Loch's Feather source.
/// </summary>
[TestFixture]
public class ReportedFixesTests
{
    private const short DarkPhoenixNumber = 77;
    private const short IcarusNumber = 10;

    private static readonly Type[] StatCommands =
    [
        typeof(AddStrengthStatChatCommandPlugIn),
        typeof(AddAgilityStatChatCommandPlugIn),
        typeof(AddVitalityStatChatCommandPlugIn),
        typeof(AddEnergyStatChatCommandPlugIn),
        typeof(AddCommandStatChatCommandPlugIn),
    ];

    /// <summary>
    /// The five single-word stat commands are no longer switched off for new databases.
    /// </summary>
    [Test]
    public void StatCommandsAreNotDisabledByDefault()
    {
        foreach (var command in StatCommands)
        {
            Assert.That(command.IsAssignableTo(typeof(IDisabledByDefault)), Is.False, command.Name);
        }
    }

    /// <summary>
    /// The update switches the commands on in an existing database and narrows Flame, leaving other plug-ins alone.
    /// </summary>
    [Test]
    public async Task UpdateEnablesCommandsAndNarrowsFlame()
    {
        using var context = new InMemoryPersistenceContextProvider().CreateNewContext();
        var configuration = context.CreateNew<GameConfiguration>();
        var commandConfigurations = StatCommands.Select(type => AddPlugInConfiguration(context, configuration, type.GUID, false)).ToList();
        var other = AddPlugInConfiguration(context, configuration, Guid.NewGuid(), false);
        var flame = context.CreateNew<Skill>();
        flame.Number = 5;
        flame.AreaSkillSettings = context.CreateNew<AreaSkillSettings>();
        flame.AreaSkillSettings.TargetAreaDiameter = 5.0f;
        configuration.Skills.Add(flame);

        await new ReportedFixesUpdatePlugIn().ApplyUpdateAsync(context, configuration);

        Assert.That(commandConfigurations.All(c => c.IsActive), Is.True);
        Assert.That(other.IsActive, Is.False);
        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(2.0f));
    }

    /// <summary>
    /// A new Season 6 database only drops Loch's Feather from Dark Phoenix, while the Crest of Monarch group stays map-wide
    /// and the quest feather of the third quest is still dropped by Dark Phoenix.
    /// </summary>
    [Test]
    public async Task NewDatabaseLimitsLochsFeatherToDarkPhoenix()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).First();
        var icarus = configuration.Maps.First(map => map.Number == IcarusNumber && map.Discriminator == 0);
        var feather = icarus.DropItemGroups.Single(group => group.Description == "Loch's Feather");
        var crest = icarus.DropItemGroups.Single(group => group.Description == "Crest of Monarch");

        Assert.That(feather.Monster?.Number, Is.EqualTo(DarkPhoenixNumber));
        Assert.That(crest.Monster, Is.Null);
        Assert.That(
            configuration.Monsters.SelectMany(monster => monster.Quests).SelectMany(quest => quest.RequiredItems).Any(requirement => requirement.Item?.Number == 67 && requirement.DropItemGroup?.Monster?.Number == DarkPhoenixNumber),
            Is.True);
    }

    /// <summary>
    /// The existing database receives the update exactly once, and later edits are kept.
    /// </summary>
    [Test]
    public async Task StartupInstallerUpdatesAnExistingDatabaseOnce()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        await new SeasonSixDataInitialization(contextProvider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false)
            .ConfigureAwait(false);

        using var context = contextProvider.CreateNewContext();
        var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var flame = configuration.Skills.Single(skill => skill.Number == 5);
        flame.AreaSkillSettings!.TargetAreaDiameter = 5.0f;
        foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false))
                     .Where(update => update.Version == (int)UpdateVersion.ReportedFixes)
                     .ToArray())
        {
            await context.DeleteAsync(update).ConfigureAwait(false);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.That(await ReportedFixesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.True);
        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(2.0f));

        flame.AreaSkillSettings.TargetAreaDiameter = 3.0f;
        await context.SaveChangesAsync().ConfigureAwait(false);
        Assert.That(await ReportedFixesInstaller.ApplyIfNeededAsync(contextProvider).ConfigureAwait(false), Is.False);
        Assert.That(flame.AreaSkillSettings.TargetAreaDiameter, Is.EqualTo(3.0f));
    }

    private static PlugInConfiguration AddPlugInConfiguration(IContext context, GameConfiguration configuration, Guid typeId, bool isActive)
    {
        var plugInConfiguration = context.CreateNew<PlugInConfiguration>();
        plugInConfiguration.TypeId = typeId;
        plugInConfiguration.IsActive = isActive;
        configuration.PlugInConfigurations.Add(plugInConfiguration);
        return plugInConfiguration;
    }
}
