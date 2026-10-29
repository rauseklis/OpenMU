// <copyright file="ChaosItemUpgradeRatesDatabaseTests.cs" company="MUnique">
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
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tests.AuctionHouse;
using Npgsql;

/// <summary>Verifies the real startup installer against a disposable database and shifted server.</summary>
[TestFixture]
[NonParallelizable]
public class ChaosItemUpgradeRatesDatabaseTests
{
    /// <summary>Simulates old rates, starts the server, and reloads the approved persisted rates.</summary>
    [Test]
    public async Task ExistingRatesAreUpgradedDuringIsolatedStartup()
    {
        var runtime = Environment.GetEnvironmentVariable("CHAOS_RATES_SMOKE_RUNTIME");
        if (runtime is null)
        {
            Assert.Ignore("Set CHAOS_RATES_SMOKE_RUNTIME and AUCTION_TEST_CONNECTION_STRING for the disposable fixture.");
        }

        var connection = new NpgsqlConnectionStringBuilder(AuctionPostgresTestEnvironment.ConnectionString);
        Assert.That(connection.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(connection.Port, Is.EqualTo(55452));
        Assert.That(connection.Database, Does.StartWith("chaos_rates_ci_"));
        var directory = Path.GetFullPath(runtime!);
        var settings = XDocument.Load(Path.Combine(directory, "ConnectionSettings.xml"))
            .Descendants().Where(element => element.Name.LocalName == "ConnectionString").ToArray();
        Assert.That(settings.Length, Is.GreaterThanOrEqualTo(7));
        Assert.That(settings.All(element => new NpgsqlConnectionStringBuilder(element.Value).EquivalentTo(connection)), Is.True);

        AuctionPostgresTestEnvironment.EnsureConfigured();
        JsonConverterRegistry.RegisterConverter(new LocalizedStringJsonConverter());
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(2, false);
        await using (var data = new EntityDataContext())
        {
            await data.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-10000;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
                UPDATE config."SimpleCraftingSettings" s SET "SuccessPercent"=99, "SuccessPercentageAdditionForLuck"=25
                FROM config."ItemCrafting" c WHERE c."SimpleCraftingSettingsId"=s."Id" AND c."Number" IN (3,4,22,23,49,50);
                DELETE FROM config."ConfigurationUpdate" WHERE "Version"=161;
                UPDATE config."ConfigurationUpdateState" SET "CurrentInstalledVersion"=160;
                """);
        }

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(Path.Combine(directory, "MUnique.OpenMU.Startup.dll"));
        start.ArgumentList.Add("-autostart");
        start.ArgumentList.Add("-adminpanel:disabled");
        start.Environment["DB_HOST"] = "127.0.0.1";
        start.Environment["DB_ADMIN_USER"] = connection.Username;
        start.Environment.Remove("DB_ADMIN_PW");
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:18112";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the isolated Chaos rates server.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            var ready = false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            while (!process.HasExited && !timeout.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", 34406, timeout.Token);
                    var greeting = new byte[4];
                    await client.GetStream().ReadExactlyAsync(greeting, timeout.Token);
                    Assert.That(greeting[0], Is.EqualTo(0xC1));
                    ready = true;
                    break;
                }
                catch (SocketException)
                {
                    await Task.Delay(500, timeout.Token);
                }
            }

            Assert.That(ready, Is.True, "The isolated server did not start its shifted listener.");
            await using var sql = new NpgsqlConnection(connection.ConnectionString);
            await sql.OpenAsync();
            await using var command = sql.CreateCommand();
            command.CommandText = """
                SELECT c."Number",s."SuccessPercent",s."SuccessPercentageAdditionForLuck"
                FROM config."ItemCrafting" c JOIN config."SimpleCraftingSettings" s ON s."Id"=c."SimpleCraftingSettingsId"
                WHERE c."Number" IN (3,4,22,23,49,50) ORDER BY c."Number";
                """;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                int[] expected = [60, 60, 55, 55, 20, 20];
                var count = 0;
                while (await reader.ReadAsync())
                {
                    Assert.That(Convert.ToInt32(reader.GetValue(1)), Is.EqualTo(expected[count++]));
                    Assert.That(reader.GetInt32(2), Is.EqualTo(20));
                }

                Assert.That(count, Is.EqualTo(6));
            }

            command.CommandText = "SELECT count(*) FROM config.\"ConfigurationUpdate\" WHERE \"Version\"=161 AND \"InstalledAt\" IS NOT NULL;";
            Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(1L));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "chaos-rates-smoke-output.log"), await output);
            await File.WriteAllTextAsync(Path.Combine(directory, "chaos-rates-smoke-errors.log"), await errors);
        }
    }
}
