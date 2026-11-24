// <copyright file="ResetExperienceDecayPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lowers the experience a character gains with every reset: the configured rate (for example 500x) stays for the
/// first resets, then it drops by a fixed amount per reset down to a minimum rate. It only applies to the
/// configured game server.
/// </summary>
[PlugIn]
[Display(Name = "Reset experience decay", Description = "Lowers the experience rate of a character with every reset after the first few, only on the configured game server (default: the 500x server).")]
[Guid("6B1F4D2A-8C37-4E59-A0D6-3F7E9B5C2A81")]
public sealed class ResetExperienceDecayPlugIn : IExperienceCalculationPlugIn, ISupportCustomConfiguration<ResetExperienceDecayConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc />
    public ResetExperienceDecayConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new ResetExperienceDecayConfiguration();

    /// <inheritdoc />
    public ValueTask CalculateExperienceAsync(Player player, ExperienceCalculationArgs args)
    {
        var configuration = this.Configuration;
        if (configuration is null
            || player.Attributes is not { } attributes
            || (configuration.OnlyServerId is { } onlyServerId && player.CurrentMap?.ServerId != onlyServerId))
        {
            return ValueTask.CompletedTask;
        }

        args.Experience *= GetFactor(configuration, (int)attributes[Stats.Resets]);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets the factor by which the experience is multiplied for a character with the given reset count.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="resets">The number of resets of the character.</param>
    /// <returns>The factor; 1 means no change.</returns>
    internal static double GetFactor(ResetExperienceDecayConfiguration configuration, int resets)
    {
        if (configuration.BaseRate <= 0 || resets <= configuration.ResetsWithoutDecay)
        {
            return 1.0;
        }

        var rate = configuration.BaseRate - (configuration.DecreasePerReset * (resets - configuration.ResetsWithoutDecay));
        rate = Math.Min(Math.Max(rate, configuration.MinimumRate), configuration.BaseRate);
        return rate / configuration.BaseRate;
    }
}
