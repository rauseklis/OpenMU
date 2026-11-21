// <copyright file="BuildIdentity.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Records which client build identifiers (the version and serial fields of the login) connect, and compares them
/// with the list of allowed builds. For now this only observes: it logs and counts, it never refuses a login.
/// </summary>
public static class BuildIdentity
{
    private const int MaximumRememberedBuilds = 1000;

    private static readonly ConcurrentDictionary<string, byte> LoggedBuilds = new(StringComparer.Ordinal);

    /// <summary>
    /// Compares a client's serial with the allowed builds of the configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="serial">The serial the client sent.</param>
    /// <returns>The result of the comparison.</returns>
    public static BuildCheckResult Check(AntiCheatConfiguration configuration, string serial)
    {
        if (configuration.BuildMode == AntiCheatMode.Off || configuration.AllowedBuildSerials.Count == 0)
        {
            return BuildCheckResult.NotChecked;
        }

        return configuration.AllowedBuildSerials.Contains(serial, StringComparer.Ordinal)
            ? BuildCheckResult.Known
            : BuildCheckResult.Unknown;
    }

    /// <summary>
    /// Observes the build identifier of a login attempt. Never refuses anything: the result only decides
    /// whether something is logged.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serial">The serial the client sent.</param>
    /// <param name="version">The resolved client version.</param>
    /// <param name="address">The address of the client.</param>
    /// <returns>The result of the comparison.</returns>
    public static BuildCheckResult Observe(AntiCheatConfiguration configuration, ILogger logger, string serial, string version, string address)
    {
        if (configuration.BuildMode == AntiCheatMode.Off)
        {
            return BuildCheckResult.NotChecked;
        }

        var result = Check(configuration, serial);
        AntiCheatState.BuildsObserved.Add(1);
        if (result == BuildCheckResult.Unknown)
        {
            AntiCheatState.BuildsUnknown.Add(1);
        }

        // Each distinct build is logged once per process, so that the log shows which builds are in use without flooding.
        var key = serial + "|" + version + "|" + result;
        if (LoggedBuilds.Count < MaximumRememberedBuilds && LoggedBuilds.TryAdd(key, 0))
        {
            logger.LogInformation(
                "[AC] rule=build.seen mode={Mode} result={Result} serial={Serial} version={Version} ip={Ip} action=allow",
                configuration.BuildMode,
                result,
                Printable(serial),
                version,
                address);
        }

        return result;
    }

    private static string Printable(string value)
    {
        var chars = value.Where(c => c >= ' ' && c < 127).Take(32).ToArray();
        return new string(chars);
    }
}
