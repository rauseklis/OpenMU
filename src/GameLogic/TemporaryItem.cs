// <copyright file="TemporaryItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.Persistence;

/// <summary>
/// A temporary item which is not persistent (yet).
/// </summary>
/// <seealso cref="MUnique.OpenMU.DataModel.Entities.Item" />
public sealed class TemporaryItem : Item
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryItem"/> class.
    /// </summary>
    public TemporaryItem()
    {
        this.ItemOptions = new List<ItemOptionLink>();
        this.ItemSetGroups = new List<ItemOfItemSet>();
    }

    /// <summary>
    /// Gets the identifier this item keeps when it becomes persistent. It exists from the moment the item is created, so the
    /// item can be followed from its first appearance (for example a monster drop on the ground) into an inventory.
    /// </summary>
    public Guid TrackingId { get; } = Guid.NewGuid();

    /// <summary>
    /// Makes this item persistent.
    /// </summary>
    /// <param name="persistenceContext">The persistence context where the item should be added.</param>
    /// <returns>The persistent item.</returns>
    public Item MakePersistent(IContext persistenceContext)
    {
        var persistentItem = persistenceContext.CreateNew<Item>();
        if (persistentItem is MUnique.OpenMU.Persistence.IIdentifiable identifiable)
        {
            identifiable.Id = this.TrackingId;
        }

        persistentItem.AssignValues(this);
        return persistentItem;
    }
}