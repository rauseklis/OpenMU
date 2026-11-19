// <copyright file="ConnectionTracker.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Tracks the connections of all game servers: concurrent and recent connections per address, connections
/// which did not log in yet, and connections which are silent.
/// </summary>
public sealed class ConnectionTracker
{
    private readonly object _sync = new();
    private readonly Dictionary<string, int> _perAddress = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> _preLoginPerServer = new();
    private readonly HashSet<TrackedConnection> _all = new();
    private readonly FailureWindows _recent;
    private readonly Func<DateTime> _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTracker"/> class.
    /// </summary>
    /// <param name="clock">The clock (UTC).</param>
    public ConnectionTracker(Func<DateTime>? clock = null)
    {
        this._clock = clock ?? (() => DateTime.UtcNow);
        this._recent = new FailureWindows(this._clock);
    }

    /// <summary>Gets the number of tracked connections.</summary>
    public int Count
    {
        get
        {
            lock (this._sync)
            {
                return this._all.Count;
            }
        }
    }

    /// <summary>
    /// Normalizes an address (maps IPv4-in-IPv6 to IPv4).
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The normalized string.</returns>
    public static string Normalize(System.Net.IPAddress address)
    {
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Registers a new connection and says whether it is within the limits. The connection is tracked in every case
    /// (the caller must call <see cref="Release"/> when it ends); the caller decides, depending on the mode, whether to refuse it.
    /// </summary>
    /// <param name="address">The normalized remote address.</param>
    /// <param name="serverId">The game server id.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connection">The tracked connection.</param>
    /// <returns>The decision.</returns>
    public ConnectionDecision TryAccept(string address, int serverId, AntiCheatConfiguration configuration, out TrackedConnection connection)
    {
        var now = this._clock();
        var recent = this._recent.Add(address, TimeSpan.FromMinutes(1));
        connection = new TrackedConnection(address, serverId, now);

        lock (this._sync)
        {
            this._perAddress.TryGetValue(address, out var concurrent);
            this._preLoginPerServer.TryGetValue(serverId, out var preLogin);

            var decision = ConnectionDecision.Allow;
            if (concurrent >= configuration.ConnectionMaxConcurrentPerIp)
            {
                decision = ConnectionDecision.TooManyForAddress;
            }
            else if (recent > configuration.ConnectionMaxNewPerIpPerMinute)
            {
                decision = ConnectionDecision.TooManyRecentForAddress;
            }
            else if (preLogin >= configuration.ConnectionMaxPreLoginPerServer)
            {
                decision = ConnectionDecision.TooManyPreLogin;
            }

            this._perAddress[address] = concurrent + 1;
            this._preLoginPerServer[serverId] = preLogin + 1;
            this._all.Add(connection);
            return decision;
        }
    }

    /// <summary>
    /// Marks the connection as logged in.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public void MarkLoggedIn(TrackedConnection connection)
    {
        lock (this._sync)
        {
            if (!connection.LoggedIn && this._all.Contains(connection))
            {
                connection.LoggedIn = true;
                this.DecrementPreLogin(connection.ServerId);
            }
        }
    }

    /// <summary>
    /// Forgets a connection which ended.
    /// </summary>
    /// <param name="connection">The connection.</param>
    public void Release(TrackedConnection connection)
    {
        lock (this._sync)
        {
            if (!this._all.Remove(connection))
            {
                return;
            }

            if (this._perAddress.TryGetValue(connection.Address, out var count))
            {
                if (count <= 1)
                {
                    this._perAddress.Remove(connection.Address);
                }
                else
                {
                    this._perAddress[connection.Address] = count - 1;
                }
            }

            if (!connection.LoggedIn)
            {
                this.DecrementPreLogin(connection.ServerId);
            }
        }
    }

    /// <summary>
    /// Finds connections which did not log in in time, or are silent for too long.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The connections with the reason (<c>prelogin</c> or <c>idle</c>).</returns>
    public IReadOnlyList<(TrackedConnection Connection, string Reason)> FindExpired(AntiCheatConfiguration configuration)
    {
        var now = this._clock();
        var result = new List<(TrackedConnection, string)>();
        lock (this._sync)
        {
            foreach (var connection in this._all)
            {
                if (!connection.LoggedIn)
                {
                    if (configuration.ConnectionPreLoginTimeoutSeconds > 0
                        && now - connection.AcceptedUtc > TimeSpan.FromSeconds(configuration.ConnectionPreLoginTimeoutSeconds))
                    {
                        result.Add((connection, "prelogin"));
                    }
                }
                else if (configuration.IdleNoPacketTimeoutSeconds > 0
                         && now - connection.LastPacketUtc > TimeSpan.FromSeconds(configuration.IdleNoPacketTimeoutSeconds))
                {
                    result.Add((connection, "idle"));
                }
            }
        }

        this._recent.Sweep(TimeSpan.FromMinutes(2));
        return result;
    }

    private void DecrementPreLogin(int serverId)
    {
        if (this._preLoginPerServer.TryGetValue(serverId, out var count))
        {
            this._preLoginPerServer[serverId] = Math.Max(0, count - 1);
        }
    }
}
