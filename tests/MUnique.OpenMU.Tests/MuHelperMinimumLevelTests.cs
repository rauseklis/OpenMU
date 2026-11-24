// <copyright file="MuHelperMinimumLevelTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Text.Json;
using MUnique.OpenMU.GameLogic.MuHelper;

/// <summary>
/// Tests the minimum level of the MU Helper per game server.
/// </summary>
[TestFixture]
public class MuHelperMinimumLevelTests
{
    /// <summary>
    /// By default only the first server (id 0) requires level 50; all others keep the general minimum.
    /// </summary>
    [Test]
    public void OnlyTheFirstServerRequiresLevelFifty()
    {
        var configuration = new MuHelperConfiguration();

        Assert.That(configuration.GetMinLevel(0), Is.EqualTo(50));
        Assert.That(configuration.GetMinLevel(1), Is.EqualTo(1));
        Assert.That(configuration.GetMinLevel(2), Is.EqualTo(1));
    }

    /// <summary>
    /// A configuration which was stored before this setting existed gets the defaults.
    /// </summary>
    [Test]
    public void StoredConfigurationWithoutTheNewSettingsKeepsTheDefaults()
    {
        var configuration = JsonSerializer.Deserialize<MuHelperConfiguration>("{\"MinLevel\":1,\"MaxLevel\":400}")!;

        Assert.That(configuration.GetMinLevel(0), Is.EqualTo(50));
        Assert.That(configuration.GetMinLevel(1), Is.EqualTo(1));
    }

    /// <summary>
    /// A general minimum above the server specific one still applies.
    /// </summary>
    [Test]
    public void GeneralMinimumStillApplies()
    {
        var configuration = new MuHelperConfiguration { MinLevel = 80 };

        Assert.That(configuration.GetMinLevel(0), Is.EqualTo(80));
        Assert.That(configuration.GetMinLevel(1), Is.EqualTo(80));
    }
}
