// <copyright file="DisabledSocketItemCleanup.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Removes only legacy socket data, retaining item identities and every ordinary item property.
/// </summary>
public static class DisabledSocketItemCleanup
{
    /// <summary>
    /// Clears old socket counts and socket-specific option links for the selected configuration.
    /// </summary>
    /// <param name="context">The persistence context.</param>
    /// <param name="configuration">The selected configuration.</param>
    /// <returns>The number of affected items and removed option links.</returns>
    public static async ValueTask<(int Items, int Options)> ApplyAsync(IContext context, GameConfiguration configuration)
    {
        var definitions = configuration.Items.Select(item => item.GetId()).ToHashSet();
        var affected = 0;
        var removed = 0;
        foreach (var item in (await context.GetAsync<Item>().ConfigureAwait(false))
            .Where(item => item.Definition is { } definition && definitions.Contains(definition.GetId())))
        {
            var socketOptions = item.ItemOptions.Where(link => SocketOptionPolicy.IsSocketOption(link.ItemOption?.OptionType)).ToArray();
            if (item.SocketCount == 0 && socketOptions.Length == 0)
            {
                continue;
            }

            item.SocketCount = 0;
            foreach (var link in socketOptions)
            {
                item.ItemOptions.Remove(link);
                await context.DeleteAsync(link).ConfigureAwait(false);
                removed++;
            }

            affected++;
        }

        return (affected, removed);
    }
}
