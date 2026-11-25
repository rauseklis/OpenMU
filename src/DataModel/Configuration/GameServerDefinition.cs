// <copyright file="GameServerDefinition.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DataModel.Configuration;

using System.Globalization;
using MUnique.OpenMU.Annotations;

/// <summary>
/// Defines the configuration of a game server.
/// </summary>
[AggregateRoot]
[Cloneable]
public partial class GameServerDefinition
{
    /// <summary>
    /// Gets or sets the server identifier.
    /// </summary>
    public byte ServerID { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the experience rate for the specific server.
    /// Be aware that this multiplies with the <see cref="MUnique.OpenMU.DataModel.Configuration.GameConfiguration.ExperienceRate"/>.
    /// </summary>
    public float ExperienceRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether PVP is enabled on this server.
    /// </summary>
    public bool PvpEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this server rolls configured small-bonus drops.
    /// Disabled by default, even when maps share small-bonus groups with another server.
    /// </summary>
    public bool SmallBonusDropsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the combined bonus chance, split equally between eligible groups.
    /// Null preserves legacy absolute group chances. Reload the server after changing it.
    /// </summary>
    public double? SmallBonusDropChance { get; set; }

    /// <summary>
    /// Gets or sets absolute per-bonus drop chances for this server only, as comma separated
    /// "bonus level=chance" pairs, for example "1=0.0033,5=0.03". The bonus level is the item level of the
    /// small-bonus drop group (1 Star, 2 Firecracker, 3 Heart of Love, 5 Silver Medal, 6 Gold Medal, 7 Box of Heaven).
    /// Chances are fractions from 0 to 1. Levels which are not listed keep the chance of their group.
    /// Null keeps the chances of the shared groups. It is ignored when <see cref="SmallBonusDropChance"/> is set.
    /// Reload the server after changing it.
    /// </summary>
    public string? SmallBonusLevelChances { get; set; }

    /// <summary>
    /// Gets or sets the experience rate which the zen dropped by monsters is based on, for this server only.
    /// Zen from kills is the gained experience plus a small base amount. When this is set, the experience part
    /// is scaled by this value divided by <see cref="ExperienceRate"/>, so a server with a high experience rate
    /// can drop zen as if its rate were lower (for example 10 on a 500x server drops what a 10x server would).
    /// Null keeps the normal calculation. Reload the server after changing it.
    /// </summary>
    public float? ZenDropExperienceRate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Fruits cannot be sold to NPC merchants on this server.
    /// Trading, the auction house and storage are not affected. Disabled by default.
    /// Reload the server after changing it.
    /// </summary>
    public bool FruitNpcSaleDisabled { get; set; }

    /// <summary>
    /// Gets or sets the server configuration.
    /// </summary>
    [Required]
    public virtual GameServerConfiguration? ServerConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the game configuration.
    /// </summary>
    [Required]
    public virtual GameConfiguration? GameConfiguration { get; set; }

    /// <summary>
    /// Gets or sets the endpoints of the game server.
    /// </summary>
    [MemberOfAggregate]
    public virtual ICollection<GameServerEndpoint> Endpoints { get; protected set; } = null!;

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "#{0} - {1}", this.ServerID, this.Description);
    }
}
