// <copyright file="IConnectionTracked.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Implemented by players which have a tracked network connection.
/// </summary>
public interface IConnectionTracked
{
    /// <summary>
    /// Gets the tracked connection, if any.
    /// </summary>
    TrackedConnection? TrackedConnection { get; }
}
