// <copyright file="ConfigureBloodCastleRewardBalanceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Events;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Installs fixed Blood Castle experience and repairs empty Ribbon reward pools.
/// </summary>
[PlugIn]
[Guid("8EF6A3F0-C1A9-4A14-9D28-A79CBDAE81C5")]
public sealed class ConfigureBloodCastleRewardBalanceUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Blood Castle experience and Ribbon payout repair";

    /// <inheritdoc />
    public override string Description => "Sets approved fixed BC one through seven experience and restores empty standard Ribbon payout pools.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.BloodCastleRewardBalance;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new BloodCastleRewardBalance(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
