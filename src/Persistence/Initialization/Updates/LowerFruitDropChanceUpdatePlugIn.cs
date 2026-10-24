// <copyright file="LowerFruitDropChanceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Sets the chance of each fruit drop of Arena, Icarus, Aida and Kanturu to <see cref="FruitDropGroups.ChancePerFruit"/>,
/// which is three times less than the first version of these drops had.
/// </summary>
[PlugIn]
[Guid("6F2B8D41-93CA-4E57-B0D6-1A7E54C8F392")]
public sealed class LowerFruitDropChanceUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Lower the fruit drop chance";

    /// <inheritdoc />
    public override string Description => "Lowers the chance of each fruit drop (Energy, Vitality, Agility, Strength, Command) in Arena, Icarus, Aida and Kanturu from 3 % to 1 % per item drop, three times less often.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.LowerFruitDropChance;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        FruitDropGroups.EnsureFor(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
