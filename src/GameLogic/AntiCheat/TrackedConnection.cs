// <copyright file="TrackedConnection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

using System.Threading;

/// <summary>
/// A tracked connection.
/// </summary>
public sealed class TrackedConnection
{
    private long _lastPacketTicks;
    private volatile bool _loggedIn;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackedConnection"/> class.
    /// </summary>
    /// <param name="address">The normalized remote address.</param>
    /// <param name="serverId">The game server id.</param>
    /// <param name="acceptedUtc">When the connection was accepted.</param>
    public TrackedConnection(string address, int serverId, DateTime acceptedUtc)
    {
        this.Address = address;
        this.ServerId = serverId;
        this.AcceptedUtc = acceptedUtc;
        this._lastPacketTicks = acceptedUtc.Ticks;
    }

    /// <summary>Gets the normalized remote address.</summary>
    public string Address { get; }

    /// <summary>Gets the game server id.</summary>
    public int ServerId { get; }

    /// <summary>Gets the time the connection was accepted.</summary>
    public DateTime AcceptedUtc { get; }

    /// <summary>Gets or sets a value indicating whether the connection logged in.</summary>
    public bool LoggedIn
    {
        get => this._loggedIn;
        set => this._loggedIn = value;
    }

    /// <summary>Gets the time of the last received packet.</summary>
    public DateTime LastPacketUtc => new(Interlocked.Read(ref this._lastPacketTicks), DateTimeKind.Utc);

    /// <summary>Gets or sets the action which closes the connection.</summary>
    public Func<ValueTask>? Close { get; set; }

    /// <summary>
    /// Remembers that a packet was just received.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    public void Touch(DateTime nowUtc) => Interlocked.Exchange(ref this._lastPacketTicks, nowUtc.Ticks);
}
