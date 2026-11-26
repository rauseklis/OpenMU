// <copyright file="RequestFilter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// Applies the <see cref="RequestMode"/> of a player to requests which other players send to him.
/// Nothing is filtered when the server has the request filter switched off.
/// </summary>
public static class RequestFilter
{
    /// <summary>
    /// Gets a value indicating whether the <paramref name="target"/> refuses all requests (<see cref="RequestMode.Off"/>).
    /// </summary>
    /// <param name="target">The player who would receive the request.</param>
    /// <returns><c>True</c>, if requests to the player have to be refused; otherwise, <c>false</c>.</returns>
    public static bool IsRefusing(Player target)
    {
        return !target.GameContext.RequestFilterDisabled && target.RequestMode == RequestMode.Off;
    }

    /// <summary>
    /// Gets a value indicating whether the <paramref name="target"/> accepts party requests on its own (<see cref="RequestMode.Auto"/>).
    /// </summary>
    /// <param name="target">The player who would receive the party request.</param>
    /// <returns><c>True</c>, if party requests to the player are accepted automatically; otherwise, <c>false</c>.</returns>
    public static bool IsAcceptingPartyAutomatically(Player target)
    {
        return !target.GameContext.RequestFilterDisabled && target.RequestMode == RequestMode.Auto;
    }

    /// <summary>
    /// Refuses the request of the <paramref name="requester"/> if the <paramref name="target"/> has the requests switched off.
    /// The requester gets a message, the target gets nothing.
    /// </summary>
    /// <param name="requester">The player who sends the request.</param>
    /// <param name="target">The player who would receive the request.</param>
    /// <returns><c>True</c>, if the request was refused and must not be sent on; otherwise, <c>false</c>.</returns>
    public static async ValueTask<bool> TryRefuseAsync(Player requester, Player target)
    {
        if (!IsRefusing(target))
        {
            return false;
        }

        await requester.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.PlayerDoesNotAcceptRequests), target.Name).ConfigureAwait(false);
        return true;
    }
}
