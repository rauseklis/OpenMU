// <copyright file="MonsterBalanceUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The x5 server gets its own monster scaling, items and zen drop half as often from ordinary monsters,
/// and excellent rings and pendants are removed from all drops.
/// </summary>
[PlugIn]
[Guid("7E2B9C54-1D3A-4A86-9F07-5B8C6D1E4A93")]
public sealed class MonsterBalanceUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Monster balance";

    /// <inheritdoc />
    public override string Description => "The x5 server gets its own monster scaling, common item and zen drop chances halved, no excellent rings or pendants from drops.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.MonsterBalance;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var balance = new VersionSeasonSix.MonsterBalance(context, gameConfiguration);
        balance.EnsureScalerConfiguration();
        balance.Initialize();
        return ValueTask.CompletedTask;
    }
}
