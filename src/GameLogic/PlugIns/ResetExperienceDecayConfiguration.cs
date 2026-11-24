// <copyright file="ResetExperienceDecayConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Configuration for the <see cref="ResetExperienceDecayPlugIn"/>.
/// </summary>
public class ResetExperienceDecayConfiguration
{
    /// <summary>
    /// Gets or sets the internal id of the only game server on which the experience decay is applied.
    /// The default is 0, the 500x server. If empty, it is applied on every game server.
    /// </summary>
    public byte? OnlyServerId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the experience rate of the server as it is configured (the rate without any decay).
    /// </summary>
    public float BaseRate { get; set; } = 500;

    /// <summary>
    /// Gets or sets the number of resets which keep the full <see cref="BaseRate"/>. The decay starts with the next reset.
    /// </summary>
    public int ResetsWithoutDecay { get; set; } = 5;

    /// <summary>
    /// Gets or sets how much the experience rate is lowered with every reset after <see cref="ResetsWithoutDecay"/>.
    /// </summary>
    public float DecreasePerReset { get; set; } = 10;

    /// <summary>
    /// Gets or sets the lowest experience rate. The decay never goes below it.
    /// </summary>
    public float MinimumRate { get; set; } = 50;
}
