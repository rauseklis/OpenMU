// <copyright file="WebsiteApiStatusSource.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Startup;

using System.Diagnostics;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.WebsiteApi.Data;

/// <summary>
/// Tells the website API how the game servers of this process are doing.
/// </summary>
internal sealed class WebsiteApiStatusSource : IWebsiteStatusSource
{
    private readonly IDictionary<int, IGameServer> _gameServers;
    private readonly DateTimeOffset _startedAt = new(Process.GetCurrentProcess().StartTime.ToUniversalTime());

    /// <summary>
    /// Initializes a new instance of the <see cref="WebsiteApiStatusSource"/> class.
    /// </summary>
    /// <param name="gameServers">The game servers of this process.</param>
    public WebsiteApiStatusSource(IDictionary<int, IGameServer> gameServers)
    {
        this._gameServers = gameServers;
    }

    /// <inheritdoc />
    public ServerStatus GetStatus()
    {
        var started = this._gameServers.Values.Where(s => s.ServerState == ServerState.Started).ToList();
        return new ServerStatus(started.Count > 0, started.Sum(s => s.CurrentConnections), started.Sum(s => s.MaximumConnections), this._startedAt);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyCollection<string>> GetOnlineCharacterNamesAsync()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var server in this._gameServers.Values.Where(s => s.ServerState == ServerState.Started).OfType<MUnique.OpenMU.GameServer.GameServer>())
        {
            foreach (var player in await server.Context.GetPlayersAsync().ConfigureAwait(false))
            {
                if (player.SelectedCharacter?.Name is { } name)
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    /// <inheritdoc />
    public bool IsCharacterOnline(string characterName)
    {
        return this._gameServers.Values
            .Where(s => s.ServerState == ServerState.Started)
            .OfType<MUnique.OpenMU.GameServer.GameServer>()
            .Any(s => s.Context.GetPlayerByCharacterName(characterName) is not null);
    }
}
