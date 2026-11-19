// <copyright file="AntiCheatConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// The configuration of the generic protective measures (login throttle, connection limits, idle timeouts).
/// The compiled defaults are deliberately loose and every measure starts in <see cref="AntiCheatMode.Shadow"/>;
/// the values in use are set in the admin panel.
/// </summary>
public class AntiCheatConfiguration
{
    /// <summary>Gets or sets the mode of the login throttle.</summary>
    public AntiCheatMode LoginMode { get; set; } = AntiCheatMode.Shadow;

    /// <summary>Gets or sets the failures allowed for one account from one address within the window.</summary>
    public int LoginPairMaxFailures { get; set; } = 10;

    /// <summary>Gets or sets the window of <see cref="LoginPairMaxFailures"/> in seconds.</summary>
    public int LoginPairWindowSeconds { get; set; } = 600;

    /// <summary>Gets or sets how long an account/address pair is refused after reaching the limit, in seconds.</summary>
    public int LoginPairLockSeconds { get; set; } = 300;

    /// <summary>Gets or sets the failures allowed for one address (all accounts) within the window.</summary>
    public int LoginIpMaxFailures { get; set; } = 60;

    /// <summary>Gets or sets the window of <see cref="LoginIpMaxFailures"/> in seconds.</summary>
    public int LoginIpWindowSeconds { get; set; } = 600;

    /// <summary>Gets or sets how long an address is refused after reaching the limit, in seconds.</summary>
    public int LoginIpLockSeconds { get; set; } = 600;

    /// <summary>Gets or sets the failures for one account (from any address) after which answers get delayed. Never locks.</summary>
    public int LoginAccountTarpitFailures { get; set; } = 30;

    /// <summary>Gets or sets the delay in milliseconds applied by the tarpit.</summary>
    public int LoginTarpitDelayMs { get; set; } = 500;

    /// <summary>Gets or sets how many password checks may run at the same time.</summary>
    public int LoginMaxConcurrentPasswordChecks { get; set; } = 4;

    /// <summary>Gets or sets how long a login waits for a free password check slot, in milliseconds.</summary>
    public int LoginPasswordCheckQueueTimeoutMs { get; set; } = 5000;

    /// <summary>Gets or sets the factor by which the login limits grow for trusted addresses.</summary>
    public int LoginTrustedMultiplier { get; set; } = 5;

    /// <summary>Gets or sets the addresses which get <see cref="LoginTrustedMultiplier"/>.</summary>
    public List<string> LoginTrustedIps { get; set; } = new();

    /// <summary>Gets or sets the mode of the connection limits.</summary>
    public AntiCheatMode ConnectionMode { get; set; } = AntiCheatMode.Shadow;

    /// <summary>Gets or sets the concurrent connections allowed per address (all game servers together).</summary>
    public int ConnectionMaxConcurrentPerIp { get; set; } = 40;

    /// <summary>Gets or sets the new connections allowed per address within a minute.</summary>
    public int ConnectionMaxNewPerIpPerMinute { get; set; } = 120;

    /// <summary>Gets or sets the connections per game server which have not logged in yet.</summary>
    public int ConnectionMaxPreLoginPerServer { get; set; } = 300;

    /// <summary>Gets or sets after how many seconds a connection which did not log in gets closed.</summary>
    public int ConnectionPreLoginTimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets the mode of the idle timeout.</summary>
    public AntiCheatMode IdleMode { get; set; } = AntiCheatMode.Shadow;

    /// <summary>Gets or sets after how many seconds without any packet a logged-in connection gets closed.</summary>
    public int IdleNoPacketTimeoutSeconds { get; set; } = 600;
}
