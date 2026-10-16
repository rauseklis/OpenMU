// <copyright file="DarkSideCooldown.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Skills;

using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Shares the Dark Side cast interval across action instances and skill upgrades.
/// </summary>
internal sealed class DarkSideCooldown
{
    private static readonly ConditionalWeakTable<Player, DarkSideCooldown> Cooldowns = new();
    private readonly object _sync = new();
    private long? _lastCast;

    internal static bool IsDarkSide(ushort skillId) => skillId is 263 or 559;

    internal static bool TryBegin(Player player) => Cooldowns.GetValue(player, _ => new()).TryBegin(Stopwatch.GetTimestamp());

    internal bool TryBegin(long timestamp)
    {
        lock (this._sync)
        {
            if (this._lastCast is { } previous && Stopwatch.GetElapsedTime(previous, timestamp) < TimeSpan.FromSeconds(1))
            {
                return false;
            }

            this._lastCast = timestamp;
            return true;
        }
    }
}
