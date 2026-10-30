// <copyright file="DroppedItem.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Threading;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Pathfinding;
using Nito.AsyncEx;

/// <summary>
/// An item that got dropped on the ground of a map.
/// </summary>
public sealed class DroppedItem : AsyncDisposable, ILocateable
{
    private static readonly TimeSpan TimeUntilDropIsFree = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the pickup lock. Used to synchronize pickup requests from the players.
    /// </summary>
    private readonly AsyncLock _pickupLock = new();

    private readonly DateTime _dropTimestamp = DateTime.UtcNow;

    /// <summary>
    /// Indicates if the item was persistent (exists on the database) when it was dropped.
    /// If it wasn't and we clean it up, then we don't need to delete it.
    /// </summary>
    private readonly bool _wasItemPersisted;

    private Player? _dropper;

    private IEnumerable<object>? _owners;

    private Timer? _removeTimer;

    private bool _availableToPick = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="DroppedItem" /> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="position">The position where the item was dropped on the map.</param>
    /// <param name="map">The map.</param>
    /// <param name="dropper">The dropper.</param>
    public DroppedItem(Item item, Point position, GameMap map, Player dropper)
        : this(item, position, map, dropper, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DroppedItem" /> class.
    /// </summary>
    /// <param name="item">The item, which should be detached from any player persistence context.</param>
    /// <param name="position">The position where the item was dropped on the map.</param>
    /// <param name="map">The map.</param>
    /// <param name="dropper">The dropper.</param>
    /// <param name="owners">The owners.</param>
    /// <param name="wasItemPersisted">If set to <c>true</c>, the item was persisted before and exists on the database.</param>
    public DroppedItem(Item item, Point position, GameMap map, Player? dropper, IEnumerable<object>? owners, bool wasItemPersisted = false)
    {
        this.Item = item;
        this.Position = position;
        this.CurrentMap = map;
        this._dropper = dropper;
        this._owners = owners;
        this._wasItemPersisted = wasItemPersisted;
        this._removeTimer = new Timer(this.OnRemoveTimerElapsed, null, (int)map.ItemDropDuration.TotalMilliseconds, Timeout.Infinite);
    }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public Item Item { get; }

    /// <inheritdoc />
    public Point Position { get; set; }

    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    public ushort Id { get; set; }

    /// <inheritdoc/>
    public GameMap CurrentMap { get; }

    /// <summary>
    /// Gets a value indicating whether the owner-pickup priority period is still active.
    /// </summary>
    public bool IsOwnerPickupPriorityActive => DateTime.UtcNow < this._dropTimestamp.Add(TimeUntilDropIsFree);

    /// <summary>
    /// Tries to pick the item up by the specified player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>
    /// The success.
    /// StackTarget: If the success is <c>true</c>, and this is not <c>null</c>, this dropped item was stacked on an existing item of the player inventory.
    /// </returns>
    /// <remarks>
    /// Can be overwritten, for example, for quest items which dropped only for a specific player.
    /// </remarks>
    public async ValueTask<(bool Success, Item? StackTarget)> TryPickUpByAsync(Player player)
    {
        Item? stackTarget = null;
        if (!this._availableToPick)
        {
            return (false, stackTarget);
        }

        if (this.Item.IsStackable())
        {
            stackTarget = player.Inventory?.Items.FirstOrDefault(i => i.CanCompletelyStackOn(this.Item));
        }

        if (stackTarget != null)
        {
            if (await this.TryStackOnItemAsync(player, stackTarget).ConfigureAwait(false))
            {
                return (true, stackTarget);
            }

            return (false, stackTarget);
        }

        if (this.Item.Definition!.IsBoundToCharacter && !this.IsPlayerAnOwner(player))
        {
            return (false, stackTarget);
        }

        if (!this.IsPlayerAnOwner(player)
            && DateTime.UtcNow < this._dropTimestamp.Add(TimeUntilDropIsFree))
        {
            return (false, stackTarget);
        }

        return (await this.TryPickUpAsync(player).ConfigureAwait(false), stackTarget);
    }

    /// <summary>
    /// Tries to pick the item up for the Jewel Bank: the same ownership rules as a normal pickup apply, but the item does not
    /// need inventory space, and it is consumed instead of being added to the inventory.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="creditAsync">Credits the bank. It runs while the drop is locked, so it cannot happen twice for one drop; if it returns <c>false</c>, the drop stays untouched.</param>
    /// <returns><c>true</c> when the drop was credited and removed.</returns>
    public async ValueTask<bool> TryPickUpIntoBankAsync(Player player, Func<ValueTask<bool>> creditAsync)
    {
        if (!this._availableToPick
            || (this.Item.Definition!.IsBoundToCharacter && !this.IsPlayerAnOwner(player))
            || (!this.IsPlayerAnOwner(player) && DateTime.UtcNow < this._dropTimestamp.Add(TimeUntilDropIsFree)))
        {
            return false;
        }

        using (await this._pickupLock.LockAsync())
        {
            if (!this._availableToPick || !await creditAsync().ConfigureAwait(false))
            {
                return false;
            }

            this._availableToPick = false;
        }

        player.Logger.LogInformation("Item '{0}' got picked up by player '{1}' and went into the Jewel Bank.", this, player);
        await this.DisposeAndDeleteAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Determines whether the specified player is an owner of this dropped item.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> if the player is an owner; otherwise, <c>false</c>.</returns>
    public bool IsPlayerAnOwner(Player player)
    {
        return this._owners?.Contains(player) ?? true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{this.Id}: {this.Item} at {this.CurrentMap.Definition.Name} ({this.Position})";
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        var timer = this._removeTimer;
        if (timer != null)
        {
            this._removeTimer = null;
            await timer.DisposeAsync().ConfigureAwait(false);
            await this.CurrentMap.RemoveAsync(this).ConfigureAwait(false);
            this._dropper = null;
            this._owners = null;
        }

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <summary>
    /// The timer callback. It is the only place which has to be <c>async void</c>, because a timer cannot await; everything it does is awaitable
    /// in <see cref="DisposeAndDeleteAsync"/>, which callers with a result to protect (the Jewel Bank pickup) await directly.
    /// </summary>
    /// <param name="state">The timer state, not used.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "Timer callback; DisposeAndDeleteAsync catches all exceptions.")]
    private async void OnRemoveTimerElapsed(object? state)
    {
        await this.DisposeAndDeleteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes this dropped item and deletes its persisted item. Never throws, because it may run under a pooled thread without a safety net.
    /// </summary>
    private async ValueTask DisposeAndDeleteAsync()
    {
        var player = this._dropper;
        try
        {
            await this.DisposeAsync().ConfigureAwait(false);
            if (player != null)
            {
                await this.DeleteItemAsync(player).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            player?.Logger.LogError(ex, "Error during DroppedItem.DisposeAndDeleteAsync");
        }
    }

    private async ValueTask<bool> TryPickUpAsync(Player player)
    {
        player.Logger.LogDebug("Player {0} tries to pick up {1}", player, this);
        var slot = player.Inventory?.CheckInvSpace(this.Item);
        if (!slot.HasValue || slot < InventoryConstants.LastEquippableItemSlotIndex)
        {
            player.Logger.LogDebug("Inventory full, Player {0}, Item {1}", player, this);
            return false;
        }

        var itemWasTemporary = this.Item is TemporaryItem;
        using (await this._pickupLock.LockAsync())
        {
            if (!this._availableToPick)
            {
                player.Logger.LogDebug("Picked up by another player in the mean time, Player {0}, Item {1}", player, this);
                return false;
            }

            if (!itemWasTemporary)
            {
                // We already attach it here so that the next changes are in the context.
                player.PersistenceContext.Attach(this.Item);
            }

            if (!await player.Inventory!.AddItemAsync((byte)slot, this.Item).ConfigureAwait(false))
            {
                player.Logger.LogDebug("Item could not be added to the inventory, Player {0}, Item {1}", player, this);

                if (!itemWasTemporary)
                {
                    player.PersistenceContext.Detach(this.Item);
                }

                return false;
            }

            if (itemWasTemporary)
            {
                // We set the item slot in a temporary item manually, so the further logic can report to the client where the item has been added.
                // If it's temporary, it has been converted into a persistent item before it was added to the inventory. So it's different instance.
                // If it's not temporary, this step is not required because the inventory already set the ItemSlot in the same instance we're holding here.
                this.Item.ItemSlot = (byte)slot;
            }

            this._availableToPick = false;
        }

        player.Logger.LogDebug("Item '{0}' was picked up by player '{1}' and added to his inventory.", this, player);
        await this.DisposeAsync().ConfigureAwait(false);

        return true;
    }

    private async ValueTask<bool> TryStackOnItemAsync(Player player, Item stackTarget)
    {
        player.Logger.LogDebug("Player {0} tries to pick up {1}, trying to add to an existing item at slot {2}", player, this, stackTarget.ItemSlot);
        using (await this._pickupLock.LockAsync())
        {
            if (!this._availableToPick)
            {
                player.Logger.LogDebug("Picked up by another player in the mean time, Player {0}, Item {1}", player, this);
                return false;
            }

            stackTarget.Durability += this.Item.Durability;
            this._availableToPick = false;
        }

        player.Logger.LogInformation("Item '{0}' got picked up by player '{1}'. Durability of available stack {2} increased to {3}", this, player, stackTarget, stackTarget.Durability);
        await this.DisposeAndDeleteAsync().ConfigureAwait(false);
        if (player.GameContext.PlugInManager.GetPlugInPoint<PlugIns.IItemStackedPlugIn>() is { } itemStackedPlugIn)
        {
            await itemStackedPlugIn.ItemStackedAsync(player, this.Item, stackTarget).ConfigureAwait(false);
        }

        return true;
    }

    private async ValueTask DeleteItemAsync(Player player)
    {
        player.Logger.LogDebug("Item '{0}' which was dropped by player '{1}' is getting deleted.", this, player);
        if (!this._wasItemPersisted)
        {
            return;
        }

        try
        {
            var repositoryManager = player.GameContext.PersistenceContextProvider;

            // We could use here the persistence context of the dropper - but if it logged out and is not saving anymore, the deletion would not be saved.
            // So we use a new temporary persistence context instead.
            // We use a trade-context as it just focuses on the items. Otherwise, we would track a lot more items.
            using var context = repositoryManager.CreateNewTradeContext();
            context.Attach(this.Item);
            await context.DeleteAsync(this.Item).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            player.Logger.LogWarning("Exception during deleting of the item {0}: {1}\n{2}", this, e.Message, e.StackTrace);
        }

        player.GameContext.PlugInManager.GetPlugInPoint<IItemDestroyedPlugIn>()?.ItemDestroyed(this.Item);
    }
}