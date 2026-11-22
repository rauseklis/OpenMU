// <copyright file="SessionEventPublisher.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Hands <see cref="SessionEvent"/>s to the registered consumers. Never throws; failures are counted in <see cref="CombatEventHealth"/>.
/// </summary>
internal static class SessionEventPublisher
{
    /// <summary>
    /// Determines whether an event is worth publishing. A connection which never logged in (a port probe, a health check)
    /// has no account and nothing to attribute, so its disconnect is not reported.
    /// </summary>
    /// <param name="kind">The kind of the event.</param>
    /// <param name="hasAccount">Whether the player is logged in to an account.</param>
    /// <returns><see langword="true"/> if the event should be published.</returns>
    internal static bool ShouldPublish(SessionEventKind kind, bool hasAccount) => hasAccount || kind != SessionEventKind.Disconnected;

    /// <summary>
    /// Publishes an event about the session of a player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="kind">The kind of the event.</param>
    /// <param name="details">Additional text for some kinds of events.</param>
    public static void Publish(Player player, SessionEventKind kind, string details = "")
    {
        if (!ShouldPublish(kind, player.Account is not null))
        {
            return;
        }

        try
        {
            var character = player.SelectedCharacter;
            var sessionEvent = new SessionEvent(
                kind,
                DateTime.UtcNow,
                (player.Account as IIdentifiable)?.Id,
                (character as IIdentifiable)?.Id,
                character?.Name ?? string.Empty,
                (player as IHasIpAddress)?.IpAddress ?? string.Empty,
                player.ClientBuild ?? string.Empty,
                CombatEventPublisher.ControllerOf(player),
                details);
            player.GameContext.PlugInManager.GetPlugInPoint<ISessionEventPlugIn>()?.OnSessionEvent(sessionEvent);
        }
        catch (Exception ex)
        {
            CombatEventHealth.RecordFailure(ex);
        }
    }
}
