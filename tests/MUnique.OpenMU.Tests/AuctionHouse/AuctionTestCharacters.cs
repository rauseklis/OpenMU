// <copyright file="AuctionTestCharacters.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using Npgsql;

/// <summary>
/// Provides a real character with an inventory and enough Zen, for tests which bid or buy out. Bids debit Zen from the
/// bidder's inventory, so those tests need a funded character from a database which contains characters, such as the
/// populated disposable copy of the server database.
/// </summary>
public static class AuctionTestCharacters
{
    /// <summary>
    /// Finds a character with an inventory and sets its Zen to the given amount.
    /// </summary>
    /// <param name="connectionString">The disposable database connection string.</param>
    /// <param name="money">The Zen to set.</param>
    /// <returns>The character identifier, or <c>null</c> when the database has no character.</returns>
    public static async Task<Guid?> FundAsync(string connectionString, int money)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Guid? character = null;
        Guid storage;
        await using (var select = new NpgsqlCommand(
                         "SELECT c.\"Id\", c.\"InventoryId\" FROM data.\"Character\" c " +
                         "JOIN data.\"ItemStorage\" s ON s.\"Id\" = c.\"InventoryId\" LIMIT 1", connection))
        await using (var reader = await select.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                return null;
            }

            character = reader.GetGuid(0);
            storage = reader.GetGuid(1);
        }

        await using var update = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
        update.Parameters.AddWithValue("money", money);
        update.Parameters.AddWithValue("id", storage);
        await update.ExecuteNonQueryAsync();
        return character;
    }

    /// <summary>
    /// Finds characters of distinct accounts which own an inventory, and sets each one's Zen to the given amount. The
    /// characters belong to their accounts, so the Auction House ownership checks accept them.
    /// </summary>
    /// <param name="connectionString">The disposable database connection string.</param>
    /// <param name="money">The Zen to set on each inventory.</param>
    /// <param name="count">The number of owners to return.</param>
    /// <returns>The owners, or an empty list when the database has fewer linked accounts.</returns>
    public static async Task<IReadOnlyList<(Guid AccountId, Guid CharacterId)>> FundOwnedAsync(string connectionString, int money, int count)
    {
        var owners = new List<(Guid AccountId, Guid CharacterId)>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var select = new NpgsqlCommand(
                         "SELECT DISTINCT ON (c.\"AccountId\") c.\"AccountId\", c.\"Id\", c.\"InventoryId\" FROM data.\"Character\" c " +
                         "WHERE c.\"AccountId\" IS NOT NULL AND c.\"InventoryId\" IS NOT NULL ORDER BY c.\"AccountId\", c.\"Id\" LIMIT @count", connection))
        {
            select.Parameters.AddWithValue("count", count);
            await using var reader = await select.ExecuteReaderAsync();
            var storages = new List<Guid>();
            while (await reader.ReadAsync())
            {
                owners.Add((reader.GetGuid(0), reader.GetGuid(1)));
                storages.Add(reader.GetGuid(2));
            }

            await reader.CloseAsync();
            foreach (var storage in storages)
            {
                await using var update = new NpgsqlCommand("UPDATE data.\"ItemStorage\" SET \"Money\" = @money WHERE \"Id\" = @id", connection);
                update.Parameters.AddWithValue("money", money);
                update.Parameters.AddWithValue("id", storage);
                await update.ExecuteNonQueryAsync();
            }
        }

        return owners.Count == count ? owners : Array.Empty<(Guid, Guid)>();
    }
}
