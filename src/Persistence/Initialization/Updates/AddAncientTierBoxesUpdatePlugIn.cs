// <copyright file="AddAncientTierBoxesUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs the approved Ancient reward tiers without enabling monster drops.
/// </summary>
[PlugIn]
[Guid("34DE22C3-7B13-4424-A3D8-08547D491AF4")]
public sealed class AddAncientTierBoxesUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Ancient reward box tiers";

    /// <inheritdoc />
    public override string Description => "Box of Luck, Silver Box and Golden Box reward the approved low, mid and high Ancient tiers at +0.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.AncientTierBoxes;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new TriteArmors(context, gameConfiguration).Initialize();
        new RageFighterAncientSets(context, gameConfiguration).Initialize();
        new AncientRewardBoxes(context, gameConfiguration).Initialize();
        new AncientBoxShop(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
