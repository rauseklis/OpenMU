// <copyright file="PetRentalLifetime.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Pet;

using System.Threading;

/// <summary>Schedules UTC rental expiry for connected and offline-helper players using their normal mutation lock.</summary>
internal sealed class PetRentalLifetime(Player player) : IDisposable
{
    private CancellationTokenSource? _cancellation;

    private Task? _task;

    /// <summary>Re-evaluates the nearest expiry after login or inventory insertion.</summary>
    public void Schedule()
    {
        this.Dispose();
        if (player.Inventory?.Items.Any(i => i.RentalExpiresAt.HasValue) is not true)
        {
            return;
        }

        this._cancellation = new();
        var token = this._cancellation.Token;
        var character = player.SelectedCharacter;
        // Do not inherit the packet handler's reentrant persistence-lock AsyncLocal.
        using (ExecutionContext.SuppressFlow())
        {
            this._task = Task.Run(() => this.RunAsync(character, token));
        }
    }

    /// <summary>Cancels and joins the timer before releasing the player's persistence context.</summary>
    public async ValueTask StopAsync()
    {
        this.Dispose();
        if (this._task is { } task)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>Removes expired inventory items; null timestamps never expire.</summary>
    public static async ValueTask ExpireAsync(Player player)
    {
        foreach (var item in player.Inventory?.Items.Where(i => i.IsRentalExpired()).ToList() ?? [])
        {
            if (item.Definition is { Group: 13, Number: 5 })
            {
                player.RemovePetCommandManager();
            }

            await player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this._cancellation?.Cancel();
        this._cancellation?.Dispose();
        this._cancellation = null;
    }

    private async Task RunAsync(Character? character, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && player.SelectedCharacter == character)
            {
                var delay = await player.RunPersistenceExclusiveAsync(() =>
                {
                    var expiry = player.Inventory?.Items.Select(i => i.RentalExpiresAt).Min();
                    return ValueTask.FromResult(expiry - DateTimeOffset.UtcNow);
                }, cancellationToken).ConfigureAwait(false);
                if (delay is null)
                {
                    return;
                }

                // Bound the delay, not the rental term; supports long products without timer overflow.
                var remaining = delay.Value;
                await Task.Delay(remaining <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(1) : remaining > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : remaining, cancellationToken).ConfigureAwait(false);
                await player.RunPersistenceExclusiveAsync(async () =>
                {
                    if (player.SelectedCharacter == character && !cancellationToken.IsCancellationRequested)
                    {
                        await ExpireAsync(player).ConfigureAwait(false);
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Character selection, inventory changes and disconnect cancel the old schedule.
        }
        catch (Exception ex)
        {
            player.Logger.LogError(ex, "Pet rental expiry failed.");
        }
    }
}
