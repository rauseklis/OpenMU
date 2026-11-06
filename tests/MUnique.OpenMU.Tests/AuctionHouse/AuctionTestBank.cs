// <copyright file="AuctionTestBank.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests.AuctionHouse;

using MUnique.OpenMU.Persistence.EntityFramework.JewelBank;
using Npgsql;

/// <summary>
/// Seeds, clears and reads Jewel Bank balances for the PostgreSQL Auction House tests, which pay jewels and fruits from the bank.
/// </summary>
public static class AuctionTestBank
{
    /// <summary>
    /// Adds to an account's balance of a bank kind, with a matching movement so the bank still reconciles.
    /// </summary>
    /// <param name="connectionString">The connection string of the disposable database.</param>
    /// <param name="accountId">The account.</param>
    /// <param name="characterId">The character the movement is attributed to.</param>
    /// <param name="code">The bank kind code.</param>
    /// <param name="count">The count to add.</param>
    /// <returns>A task.</returns>
    public static async Task AddAsync(string connectionString, Guid accountId, Guid characterId, byte code, long count)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO data.\"JewelBankBalance\" (\"AccountId\", \"Kind\", \"Count\", \"UpdatedAt\") VALUES (@account, @kind, @count, now()) ON CONFLICT (\"AccountId\", \"Kind\") DO UPDATE SET \"Count\" = data.\"JewelBankBalance\".\"Count\" + EXCLUDED.\"Count\";"
            + " INSERT INTO data.\"JewelBankMovement\" (\"Id\", \"AccountId\", \"CharacterId\", \"OperationId\", \"Kind\", \"Delta\", \"Balance\", \"Reason\", \"CreatedAt\") VALUES (gen_random_uuid(), @account, @character, gen_random_uuid(), @kind, @count, (SELECT \"Count\" FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = @account AND \"Kind\" = @kind), 0, now());",
            connection);
        command.Parameters.AddWithValue("account", accountId);
        command.Parameters.AddWithValue("kind", (short)code);
        command.Parameters.AddWithValue("count", count);
        command.Parameters.AddWithValue("character", characterId);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes the balances and movements of the accounts.
    /// </summary>
    /// <param name="connectionString">The connection string of the disposable database.</param>
    /// <param name="accounts">The accounts.</param>
    /// <returns>A task.</returns>
    public static async Task ClearAsync(string connectionString, params Guid[] accounts)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM data.\"JewelBankMovement\" WHERE \"AccountId\" = ANY(@accounts); DELETE FROM data.\"JewelBankBalance\" WHERE \"AccountId\" = ANY(@accounts);", connection);
        command.Parameters.AddWithValue("accounts", accounts);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads an account's balance of a bank kind.
    /// </summary>
    /// <param name="accountId">The account.</param>
    /// <param name="code">The bank kind code.</param>
    /// <returns>The count.</returns>
    public static async Task<long> CountAsync(Guid accountId, byte code)
        => (await new JewelBankRepository().GetStateAsync(accountId)).CountOf(code);
}
