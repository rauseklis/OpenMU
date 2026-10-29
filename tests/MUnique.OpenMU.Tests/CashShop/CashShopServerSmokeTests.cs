// <copyright file="CashShopServerSmokeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.CashShop;

using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.Persistence.EntityFramework;
using MUnique.OpenMU.Persistence.EntityFramework.Json;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Tests.AuctionHouse;

/// <summary>Seeds a disposable server and checks a real shifted connect-server listener without logging in.</summary>
[TestFixture]
[NonParallelizable]
public class CashShopServerSmokeTests
{
    /// <summary>Starts only the isolated runtime supplied by CI, verifies readiness, and stops its own process.</summary>
    [Test]
    public async Task ShiftedServerStartsAgainstDisposableDatabase()
    {
        var runtime = Environment.GetEnvironmentVariable("CASH_SHOP_SMOKE_RUNTIME");
        if (runtime is null)
        {
            Assert.Ignore("Set CASH_SHOP_SMOKE_RUNTIME to the isolated CI runtime.");
        }

        var directory = Path.GetFullPath(runtime!);
        var connections = XDocument.Load(Path.Combine(directory, "ConnectionSettings.xml"))
            .Descendants().Where(e => e.Name.LocalName == "ConnectionString").ToArray();
        Assert.That(connections, Is.Not.Empty);
        foreach (var connection in connections)
        {
            var setting = new Npgsql.NpgsqlConnectionStringBuilder(connection.Value);
            Assert.That(setting.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(setting.Port, Is.EqualTo(55432));
            Assert.That(setting.Database, Does.StartWith("cash_shop_ci"));
        }

        var testConnection = new Npgsql.NpgsqlConnectionStringBuilder(AuctionPostgresTestEnvironment.ConnectionString);
        Assert.That(testConnection.Host, Is.EqualTo("127.0.0.1"));
        Assert.That(testConnection.Port, Is.EqualTo(55432));
        Assert.That(testConnection.Database, Does.StartWith("cash_shop_ci"));
        AuctionPostgresTestEnvironment.EnsureConfigured();
        // Match Startup.Program before loading PostgreSQL bytea values from configuration JSON.
        JsonConverterRegistry.RegisterConverter(new BinaryAsHexJsonConverter());
        var provider = new PersistenceContextProvider(NullLoggerFactory.Instance, null);
        await provider.ApplyAllPendingUpdatesAsync();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false);
        await using (var context = new EntityDataContext())
        {
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE config."ConnectServerDefinition" SET "ClientListenerPort"="ClientListenerPort"-10000;
                UPDATE config."GameServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
                UPDATE config."ChatServerEndpoint" SET "NetworkPort"="NetworkPort"-10000;
                """);
        }

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
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:18080";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start isolated server.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            var ready = false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            while (!timeout.IsCancellationRequested && !process.HasExited)
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

            Assert.That(ready, Is.True, "The isolated shifted connect server did not become ready.");
            Assert.That(process.HasExited, Is.False);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            var log = await output;
            var errorLog = await errors;
            if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                TestContext.WriteLine(log.Length > 12000 ? log[^12000..] : log);
                TestContext.WriteLine(errorLog.Length > 4000 ? errorLog[^4000..] : errorLog);
            }
        }
    }
}
