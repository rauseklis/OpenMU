// <copyright file="ExperienceRewardCapDatabaseTests.cs" company="MUnique">
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

/// <summary>Checks the changed server against a fresh disposable database and shifted listeners.</summary>
[TestFixture]
[NonParallelizable]
public class ExperienceRewardCapDatabaseTests
{
    /// <summary>Initializes an isolated database and starts the Release server without live endpoints.</summary>
    [Test]
    public async Task DisposableServerStartsWithRewardCapsAsync()
    {
        var runtime = Environment.GetEnvironmentVariable("EXP_CAP_SMOKE_RUNTIME");
        if (runtime is null)
        {
            Assert.Ignore("Set EXP_CAP_SMOKE_RUNTIME and AUCTION_TEST_CONNECTION_STRING for a disposable cluster.");
        }

        var connection = new NpgsqlConnectionStringBuilder(AuctionPostgresTestEnvironment.ConnectionString);
        Assert.That(connection.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(connection.Port, Is.EqualTo(55448));
        Assert.That(connection.Database, Does.StartWith("exp_cap_20261028_"));
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
        await using (var data = new EntityDataContext())
        {
            await data.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-9980;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-9980;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-9980;
                """).ConfigureAwait(false);
        }

        await CheckShiftedServerAsync(directory, connection).ConfigureAwait(false);
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
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:18098";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the disposable EXP-cap server.");
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
                    await client.ConnectAsync("127.0.0.1", 34426, timeout.Token).ConfigureAwait(false);
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
            await File.WriteAllTextAsync(Path.Combine(directory, "exp-cap-smoke-output.log"), await output.ConfigureAwait(false)).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(directory, "exp-cap-smoke-errors.log"), await errors.ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
