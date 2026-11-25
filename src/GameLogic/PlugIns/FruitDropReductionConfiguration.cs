// <copyright file="FruitDropReductionConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="FruitDropReductionPlugIn"/>.
/// </summary>
public class FruitDropReductionConfiguration
{
    /// <summary>
    /// Gets or sets the internal id of the only game server on which the reduction is applied.
    /// The default is 0, the 500x server. If empty, it is applied on every game server.
    /// </summary>
    public byte? OnlyServerId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the probability (0.0 to 1.0) with which a fruit drop is cancelled.
    /// The default is 0.5 which halves the effective fruit drop rate.
    /// </summary>
    public double CancelProbability { get; set; } = 0.5;
}
