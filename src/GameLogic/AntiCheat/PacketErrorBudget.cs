// <copyright file="PacketErrorBudget.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Limits what a connection can cost with packets which make a handler fail: only a few failures per minute are written to
/// the log in detail, all are counted, and a connection which exceeds the allowed number per minute is reported once per minute.
/// </summary>
public sealed class PacketErrorBudget
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _sync = new();
    private DateTime _windowStart = DateTime.MinValue;
    private int _errors;
    private int _detailed;
    private int _suppressed;
    private bool _exceededReported;

    /// <summary>
    /// Records a failed packet.
    /// </summary>
    /// <param name="now">The current time (UTC).</param>
    /// <param name="maximumPerMinute">The failures per minute after which the connection is reported as exceeding.</param>
    /// <param name="detailLogsPerMinute">How many failures per minute may be logged in detail.</param>
    /// <returns>What the caller should do.</returns>
    public PacketErrorDecision Record(DateTime now, int maximumPerMinute, int detailLogsPerMinute)
    {
        lock (this._sync)
        {
            var suppressedBefore = 0;
            if (now - this._windowStart >= Window)
            {
                suppressedBefore = this._suppressed;
                this._windowStart = now;
                this._errors = 0;
                this._detailed = 0;
                this._suppressed = 0;
                this._exceededReported = false;
            }

            this._errors++;
            var logDetail = this._detailed < detailLogsPerMinute;
            if (logDetail)
            {
                this._detailed++;
            }
            else
            {
                this._suppressed++;
            }

            var exceeded = !this._exceededReported && this._errors > maximumPerMinute;
            if (exceeded)
            {
                this._exceededReported = true;
            }

            return new PacketErrorDecision(logDetail, exceeded, suppressedBefore, this._errors);
        }
    }
}
