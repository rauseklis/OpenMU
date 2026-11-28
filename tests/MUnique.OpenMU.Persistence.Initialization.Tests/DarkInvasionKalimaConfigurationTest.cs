// <copyright file="DarkInvasionKalimaConfigurationTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json.Nodes;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Tests that a stored Dark Skeleton invasion configuration from before the Kalima monsters (like the one
/// on the live server) gets the Kalima monsters and the distance settings, and keeps its other settings.
/// </summary>
[TestFixture]
internal class DarkInvasionKalimaConfigurationTest
{
    /// <summary>
    /// The settings which are missing in the stored configuration get their default values.
    /// </summary>
    [Test]
    public void StoredConfigurationWithoutKalimaGetsTheDefaults()
    {
        var stored = DarkSkeletonInvasionConfiguration.Default;
        stored.Timetable = [new TimeOnly(21, 30)];
        stored.GameServerIds = [0, 1];
        var plugInConfiguration = new PlugInConfiguration();
        plugInConfiguration.SetConfiguration(stored, DarkInvasionBothServersUpdatePlugIn.ServerLikeReferenceHandler.Instance);

        var json = JsonNode.Parse(plugInConfiguration.CustomConfiguration!)!.AsObject();
        foreach (var newSetting in new[] { "ExtraMobs", "TownDistanceMapIds", "MinimumTownDistance", "MinimumEntranceDistance", "AllBossesKilledMessage" })
        {
            Assert.That(json.Remove(newSetting), Is.True, $"The setting {newSetting} should be stored.");
        }

        plugInConfiguration.CustomConfiguration = json.ToJsonString();

        var loaded = plugInConfiguration.GetConfiguration<DarkSkeletonInvasionConfiguration>(DarkInvasionBothServersUpdatePlugIn.ServerLikeReferenceHandler.Instance)!;
        Assert.That(loaded.Timetable, Is.EqualTo(new[] { new TimeOnly(21, 30) }), "Other settings are kept.");
        Assert.That(loaded.GameServerIds, Is.EqualTo(new byte[] { 0, 1 }));
        Assert.That(loaded.ExtraMobs.Select(m => (m.MonsterId, m.Count, string.Join(",", m.MapIds))), Is.EqualTo(new[]
        {
            ((ushort)1113, (ushort)5, "25,26"),
            ((ushort)1111, (ushort)4, "27,28"),
            ((ushort)1112, (ushort)3, "29,36"),
        }));
        Assert.That(loaded.TownDistanceMapIds, Is.EqualTo(new ushort[] { 0, 3, 51 }));
        Assert.That(loaded.MinimumTownDistance, Is.EqualTo(20));
        Assert.That(loaded.MinimumEntranceDistance, Is.EqualTo(10));
        Assert.That((string)loaded.AllBossesKilledMessage, Does.Contain("All Dark Reapers have been defeated!"));
    }
}
