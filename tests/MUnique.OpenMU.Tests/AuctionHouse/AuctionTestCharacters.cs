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
}
