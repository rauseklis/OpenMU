// <copyright file="SkillCastingDatabaseTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tests.AuctionHouse;
using Npgsql;

/// <summary>Checks the changed server against a fresh disposable database and shifted listeners.</summary>
[TestFixture]
[NonParallelizable]
public class SkillCastingDatabaseTests
{
    /// <summary>Initializes an isolated database and starts the Release server without live endpoints.</summary>
    [Test]
    public async Task DisposableServerStartsWithCastingPolicyAsync()
    {
        var runtime = Environment.GetEnvironmentVariable("SKILL_CASTING_SMOKE_RUNTIME");
        if (runtime is null)
        {
            Assert.Ignore("Set SKILL_CASTING_SMOKE_RUNTIME and AUCTION_TEST_CONNECTION_STRING for a disposable cluster.");
        }

        var connection = new NpgsqlConnectionStringBuilder(AuctionPostgresTestEnvironment.ConnectionString);
        Assert.That(connection.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(connection.Port, Is.EqualTo(55449));
        Assert.That(connection.Database, Does.StartWith("skill_casting_20261029_"));
        var directory = Path.GetFullPath(runtime!);
        var settings = XDocument.Load(Path.Combine(directory, "ConnectionSettings.xml"))
            .Descendants().Where(element => element.Name.LocalName == "ConnectionString").ToArray();
        Assert.That(settings.Length, Is.GreaterThanOrEqualTo(8));
        foreach (var setting in settings)
        {
            Assert.That(new NpgsqlConnectionStringBuilder(setting.Value).ConnectionString, Is.EqualTo(connection.ConnectionString));
        }

        AuctionPostgresTestEnvironment.EnsureConfigured();
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync().ConfigureAwait(false);
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false).ConfigureAwait(false);
        using (var context = provider.CreateNewContext())
        {
            var configuration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            VerifyAllSkillStatThresholds(configuration);
            var twistingSlash = configuration.Items.Single(item => item.Group == 12 && item.Number == 7);
            Assert.That(twistingSlash.Requirements.Any(requirement => requirement.Attribute == Stats.Level && requirement.MinimumValue == 80), Is.True);
            var darkHorse = configuration.Items.Single(item => item.Name == "Dark Horse");
            Assert.That(darkHorse.Requirements.Any(requirement => requirement.Attribute == Stats.Level && requirement.MinimumValue == 218), Is.True);
        }

        await using (var data = new EntityDataContext())
        {
            await data.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-9970;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-9970;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-9970;
                """).ConfigureAwait(false);
        }

        await CheckShiftedServerAsync(directory, connection).ConfigureAwait(false);
    }

    private static void VerifyAllSkillStatThresholds(GameConfiguration configuration)
    {
        var checkedSkills = 0;
        var checkedStats = 0;
        foreach (var skill in configuration.Skills.Where(skill => skill.QualifiedCharacters.Count > 0))
        {
            var values = skill.Requirements.Where(requirement => requirement.Attribute is not null && requirement.Attribute != Stats.Level)
                .GroupBy(requirement => requirement.Attribute!)
                .ToDictionary(group => group.Key, group => (float)group.Max(requirement => requirement.MinimumValue));
            float? Resolve(MUnique.OpenMU.AttributeSystem.AttributeDefinition attribute)
                => attribute == Stats.Level ? 1 : values.GetValueOrDefault(attribute);
            Assert.That(SkillCastingRequirements.AreMet(skill, Resolve), Is.True, $"Skill {skill.Number}: exact stat requirements at level one.");
            foreach (var attribute in values.Keys.ToArray())
            {
                var required = values[attribute];
                if (required <= 0)
                {
                    continue;
                }

                values[attribute] = required - 1;
                Assert.That(SkillCastingRequirements.AreMet(skill, Resolve), Is.False, $"Skill {skill.Number}: missing {attribute.Designation}.");
                values[attribute] = required;
                checkedStats++;
            }

            checkedSkills++;
        }

        Assert.That(checkedSkills, Is.GreaterThan(100));
        Assert.That(checkedStats, Is.GreaterThan(0));
        TestContext.Progress.WriteLine($"Validated {checkedSkills} player skills and {checkedStats} individual stat thresholds.");
    }

    private static async Task CheckShiftedServerAsync(string directory, NpgsqlConnectionStringBuilder connection)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(directory, "MUnique.OpenMU.Startup.dll"));
        start.ArgumentList.Add("-autostart");
        start.ArgumentList.Add("-adminpanel:disabled");
        start.Environment["DB_HOST"] = "127.0.0.1";
        start.Environment["DB_ADMIN_USER"] = connection.Username;
        start.Environment.Remove("DB_ADMIN_PW");
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:18109";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the disposable skill-casting server.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var ready = false;
            while (!process.HasExited && !timeout.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", 34436, timeout.Token).ConfigureAwait(false);
                    var greeting = new byte[4];
                    await client.GetStream().ReadExactlyAsync(greeting, timeout.Token).ConfigureAwait(false);
                    Assert.That(greeting[0], Is.EqualTo(0xC1));
                    ready = true;
                    break;
                }
                catch (SocketException)
                {
                    await Task.Delay(500, timeout.Token).ConfigureAwait(false);
                }
            }

            Assert.That(ready, Is.True, "The disposable server did not send its connect-server greeting.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "skill-casting-smoke-output.log"), await output.ConfigureAwait(false)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "skill-casting-smoke-errors.log"), await errors.ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
