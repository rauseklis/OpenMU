// <copyright file="AddFruitDropsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the monsters of Arena, Icarus, Aida and Kanturu drop all five fruits.
/// </summary>
[PlugIn]
[Guid("3C9E5A27-6B14-4D80-A1F3-52E8D07B94C1")]
public sealed class AddFruitDropsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Fruit drops in Arena, Icarus, Aida and Kanturu";

    /// <inheritdoc />
    public override string Description => "Gives every monster of Arena, Icarus, Aida and the three Kanturu maps a 3 % chance per item drop for each of the five fruits (Energy, Vitality, Agility, Strength, Command).";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AddFruitDrops;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        FruitDropGroups.EnsureFor(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }
}
