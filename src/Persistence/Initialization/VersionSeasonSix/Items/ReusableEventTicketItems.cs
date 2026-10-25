// <copyright file="ReusableEventTicketItems.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Defines the client's existing reusable event tickets, with remaining uses stored as durability.
/// </summary>
public sealed class ReusableEventTicketItems : InitializerBase
{
    internal const byte TicketGroup = 13;
    internal const byte DefaultUses = 10;
    internal static readonly short[] TicketNumbers = [46, 47, 48];

    /// <summary>
    /// Initializes a new instance of the <see cref="ReusableEventTicketItems"/> class.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The game configuration.</param>
    public ReusableEventTicketItems(IContext context, GameConfiguration configuration)
        : base(context, configuration)
    {
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        string[] names = ["Devil Square Ticket", "Blood Castle Ticket", "Kalima Ticket"];
        for (var index = 0; index < TicketNumbers.Length; index++)
        {
            if (this.GameConfiguration.Items.Any(item => item.Group == TicketGroup && item.Number == TicketNumbers[index]))
            {
                continue;
            }

            var item = this.Context.CreateNew<ItemDefinition>();
            item.Group = TicketGroup;
            item.Number = TicketNumbers[index];
            item.Name = names[index];
            item.Width = item.Height = 1;
            item.Durability = DefaultUses;
            item.IsTradable = item.IsDroppable = item.IsSellableToNpc = item.IsPersonalStoreSellable = item.IsRepairable = false;
            item.IsStorable = true;
            item.SetGuid(item.Group, item.Number);
            this.GameConfiguration.Items.Add(item);
        }
    }
}
