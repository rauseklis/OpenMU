// <copyright file="PacketErrorDecision.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// The answer of <see cref="PacketErrorBudget"/> for one failed packet.
/// </summary>
/// <param name="LogDetail">Whether this failure may be logged in detail (message, stack trace, packet).</param>
/// <param name="Exceeded">Whether the connection just crossed the allowed number of failures per minute (reported once per minute).</param>
/// <param name="SuppressedInPreviousWindow">How many failures of the previous minute were not logged (reported when a new minute starts).</param>
/// <param name="ErrorsInWindow">The number of failures in the current minute.</param>
public readonly record struct PacketErrorDecision(bool LogDetail, bool Exceeded, int SuppressedInPreviousWindow, int ErrorsInWindow);
