// <copyright file="WorldGameplayParityDatabaseTests.cs" company="MUnique">
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
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tests.AuctionHouse;
using Npgsql;

/// <summary>Verifies the persistent upgrade and a second server against a new task-owned cluster.</summary>
[TestFixture]
[NonParallelizable]
public class WorldGameplayParityDatabaseTests
{
    /// <summary>Seeds an isolated database, upgrades released CC rewards, reloads and starts shifted listeners.</summary>
    [Test]
    public async Task UpgradeReloadAndShiftedServerAsync()
    {
        var runtime = Environment.GetEnvironmentVariable("PARITY_SMOKE_RUNTIME");
        if (runtime is null)
        {
            Assert.Ignore("Set PARITY_SMOKE_RUNTIME and AUCTION_TEST_CONNECTION_STRING for the task-owned disposable cluster.");
        }

        var connection = new NpgsqlConnectionStringBuilder(AuctionPostgresTestEnvironment.ConnectionString);
        Assert.That(connection.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(connection.Port, Is.EqualTo(55440));
        Assert.That(connection.Database, Does.StartWith("world_parity_20261028_"));
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
            var config = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            foreach (var game in config.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle && game.GameLevel > 1))
            {
                var ancient = game.Rewards.Single(reward => reward.ItemReward!.PossibleItems.All(item => item.Group == 14 && item.Number is 11 or 123 or 124)).ItemReward!;
                ancient.ItemType = SpecialItemType.Ancient;
                ancient.Chance = .1 * game.GameLevel;
                ancient.PossibleItems.Clear();
            }

            foreach (var update in (await context.GetAsync<ConfigurationUpdate>().ConfigureAwait(false)).Where(update => update.Version == (int)UpdateVersion.ChaosCastleBoxRewards).ToArray())
            {
                await context.DeleteAsync(update).ConfigureAwait(false);
            }

            await context.SaveChangesAsync().ConfigureAwait(false);
        }

        Assert.That(await ChaosCastleBoxRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.True);
        Assert.That(await ChaosCastleBoxRewardsInstaller.ApplyIfNeededAsync(provider).ConfigureAwait(false), Is.False);
        using (var reload = provider.CreateNewContext())
        {
            var config = (await reload.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
            var worlds = (await reload.GetAsync<GameServerDefinition>().ConfigureAwait(false)).ToArray();
            Assert.That(worlds, Has.Length.EqualTo(2));
            foreach (var game in config.MiniGameDefinitions.Where(game => game.Type == MiniGameType.ChaosCastle))
            {
                Assert.That(game.Rewards.Count, Is.EqualTo(game.GameLevel == 1 ? 1 : 2));
                var jewel = game.Rewards.Single(reward => reward.ItemReward!.PossibleItems.Any(item => item.Group == 12 && item.Number == 15));
                Assert.That(jewel.ItemReward!.Chance, Is.EqualTo(.9));
                if (game.GameLevel > 1)
                {
                    var box = game.Rewards.Single(reward => reward != jewel).ItemReward!;
                    Assert.That(box.Chance, Is.EqualTo(.05 * (game.GameLevel - 1)).Within(1e-12));
                    Assert.That(box.ItemType, Is.EqualTo(SpecialItemType.RandomItem));
                    Assert.That(box.PossibleItems.Select(item => (int)item.Number), Is.EquivalentTo(game.GameLevel switch
                    {
                        <= 4 => new[] { 11 }, 5 => new[] { 11, 124 }, 6 => new[] { 124 }, _ => new[] { 124, 123 },
                    }));
                }
            }
        }

        using (var context = provider.CreateNewContext())
        {
            var servers = (await context.GetAsync<GameServerDefinition>()).OrderBy(server => server.ServerID).ToArray();
            servers[0].ExperienceRate = 50;
            servers[1].ExperienceRate = 5;
            servers[1].SmallBonusDropsEnabled = false;
            foreach (var update in (await context.GetAsync<ConfigurationUpdate>()).Where(update => update.Version == (int)UpdateVersion.WorldGameplayParity).ToArray())
            {
                await context.DeleteAsync(update);
            }
            await context.SaveChangesAsync();
        }
        Assert.That(await WorldGameplayParityInstaller.ApplyIfNeededAsync(provider), Is.True);
        Assert.That(await WorldGameplayParityInstaller.ApplyIfNeededAsync(provider), Is.False);
        using (var context = provider.CreateNewContext())
        {
            var servers = (await context.GetAsync<GameServerDefinition>()).OrderBy(server => server.ServerID).ToArray();
            Assert.That(servers.All(server => server.SmallBonusDropsEnabled && server.SmallBonusDropChance is null), Is.True);
            Assert.That(servers.Select(server => server.ExperienceRate), Is.EqualTo(new[] { 50f, 5f }));
        }
        // Seeded systems may autostart without -autostart: shift every endpoint before launching the process.
        await using (var data = new EntityDataContext())
        {
            await data.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-9990;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-9990;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-9990;
                """).ConfigureAwait(false);
        }

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(directory, "MUnique.OpenMU.Startup.dll"));
        start.ArgumentList.Add("-autostart");
        start.ArgumentList.Add("-adminpanel:disabled");
        start.Environment["DB_HOST"] = "127.0.0.1";
        start.Environment["DB_ADMIN_USER"] = connection.Username;
        start.Environment.Remove("DB_ADMIN_PW");
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:18090";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated CC server.");
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
                    await client.ConnectAsync("127.0.0.1", 34416, timeout.Token).ConfigureAwait(false);
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

            Assert.That(ready, Is.True, "The shifted disposable server did not send a greeting.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            // Keep runtime logs in the ignored build output, outside the test report.
            await File.WriteAllTextAsync(Path.Combine(directory, "cc-smoke-output.log"), await output.ConfigureAwait(false)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "cc-smoke-errors.log"), await errors.ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
