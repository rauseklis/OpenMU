// <copyright file="GuildCreateAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Guild;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Views.Guild;

/// <summary>
/// Action to create a guild.
/// </summary>
public class GuildCreateAction
{
    private const int MinimumCreatorLevel = 100;

    /// <summary>
    /// Gets the reason why the player is not allowed to create a guild, or <c>null</c> if the player is allowed.
    /// The same rule applies when the guild master npc is asked for the creation dialog and when the creation is requested.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The message for the player, or <c>null</c>.</returns>
    public static string? GetCreationRefusal(Player player)
    {
        if (player.Level < MinimumCreatorLevel)
        {
            return "Your level should be at least level 100";
        }

        if (player.GuildStatus != null)
        {
            return "You already belong to a guild";
        }

        return null;
    }

    /// <summary>
    /// Creates the guild.
    /// </summary>
    /// <param name="creator">The creator.</param>
    /// <param name="guildName">Name of the guild.</param>
    /// <param name="guildEmblem">The guild emblem.</param>
    public async ValueTask CreateGuildAsync(Player creator, string guildName, byte[] guildEmblem)
    {
        using var loggerScope = creator.Logger.BeginScope(this.GetType());
        if (creator.PlayerState.CurrentState != PlayerState.EnteredWorld)
        {
            creator.Logger.LogError($"Account {creator.Account?.LoginName} not in the right state, but {creator.PlayerState.CurrentState}.");
            return;
        }

        if (creator.OpenedNpc?.Definition?.NpcWindow != NpcWindow.GuildMaster
            || GetCreationRefusal(creator) is { } refusal)
        {
            creator.Logger.LogWarning("The guild creation was requested, but its requirements are not met.");
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
            return;
        }

        var guildServer = (creator.GameContext as IGameServerContext)?.GuildServer;
        if (guildServer is null)
        {
            creator.Logger.LogError($"No guild server available");
            return;
        }

        if (await guildServer.GuildExistsAsync(guildName).ConfigureAwait(false))
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
            return;
        }

        if (await guildServer.CreateGuildAsync(guildName, creator.SelectedCharacter!.Name, creator.SelectedCharacter.Id, guildEmblem, ((IGameServerContext)creator.GameContext).Id).ConfigureAwait(false))
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.None)).ConfigureAwait(false);
            creator.Logger.LogInformation("Guild created: [{0}], Master: [{1}]", guildName, creator.SelectedCharacter.Name);
        }
        else
        {
            await creator.InvokeViewPlugInAsync<IShowGuildCreateResultPlugIn>(p => p.ShowGuildCreateResultAsync(GuildCreateErrorDetail.GuildAlreadyExist)).ConfigureAwait(false);
        }
    }
}