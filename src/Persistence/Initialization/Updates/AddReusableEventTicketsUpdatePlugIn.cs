// <copyright file="AddReusableEventTicketsUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds reusable BC, DS and Kalima tickets and their test shop listings.
/// </summary>
[PlugIn]
[Guid("9AF153C9-C61A-4B67-8B61-067A861D5BE2")]
public sealed class AddReusableEventTicketsUpdatePlugIn : UpdatePlugInBase
{
    /// <inheritdoc />
    public override string Name => "Reusable event tickets";

    /// <inheritdoc />
    public override string Description => "Adds ten-use BC, DS and Kalima tickets to Lumen for 1,000 Zen each.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.ReusableEventTickets;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new ReusableEventTicketItems(context, gameConfiguration).Initialize();
        new ReusableEventTicketShop(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
