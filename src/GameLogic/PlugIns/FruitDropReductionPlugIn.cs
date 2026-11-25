// <copyright file="FruitDropReductionPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Reduces the effective fruit drop rate on the configured game server by cancelling a percentage of fruit drops.
/// </summary>
[PlugIn]
[Display(Name = "Fruit drop reduction", Description = "Cancels a configurable percentage of fruit drops on the configured game server (default: 50 % on the 500x server), effectively halving the drop rate.")]
[Guid("7C4E2F18-D3A9-4B65-91F0-8E6D5A3C1B47")]
public sealed class FruitDropReductionPlugIn : IMonsterItemDropPlugIn, ISupportCustomConfiguration<FruitDropReductionConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <summary>
    /// The item group of the fruits.
    /// </summary>
    private const byte FruitItemGroup = 13;

    /// <summary>
    /// The item number of the fruits.
    /// </summary>
    private const short FruitItemNumber = 15;

    private static readonly System.Threading.ThreadLocal<Random> ThreadRandom = new(() => new Random());

    /// <inheritdoc />
    public FruitDropReductionConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new FruitDropReductionConfiguration();

    /// <inheritdoc />
    public void ItemDropping(AttackableNpcBase monster, Player killer, Item item, CancelEventArgs eventArgs)
    {
        if (eventArgs.Cancel)
        {
            return;
        }

        var configuration = this.Configuration;
        if (configuration is null)
        {
            return;
        }

        if (configuration.OnlyServerId is { } id && killer.CurrentMap?.ServerId != id)
        {
            return;
        }

        if (item.Definition?.Group != FruitItemGroup || item.Definition.Number != FruitItemNumber)
        {
            return;
        }

        if (ThreadRandom.Value!.NextDouble() < configuration.CancelProbability)
        {
            eventArgs.Cancel = true;
        }
    }
}
