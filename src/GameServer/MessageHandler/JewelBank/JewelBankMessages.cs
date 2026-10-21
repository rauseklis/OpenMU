// <copyright file="JewelBankMessages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.JewelBank;

using System.Buffers.Binary;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.JewelBank;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// The shared steps of the Jewel Bank request handlers: the service, the request budget, and the responses.
/// </summary>
internal static class JewelBankMessages
{
    /// <summary>
    /// The operation type sent in an operation response.
    /// </summary>
    internal enum OperationKind : byte
    {
        /// <summary>A deposit of inventory items.</summary>
        Deposit = 0,

        /// <summary>A withdrawal as items.</summary>
        Withdraw = 1,

        /// <summary>A change of the auto-deposit choice.</summary>
        SetAutoDeposit = 2,
    }

    /// <summary>
    /// Sends the state of the bank to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="result">The result code which accompanies the state.</param>
    /// <param name="state">The state.</param>
    /// <returns>A task which completes when the state is sent.</returns>
    public static async ValueTask SendStateAsync(Player player, JewelBankResultCode result, JewelBankState state)
    {
        if (player is not RemotePlayer { Connection: { } connection })
        {
            return;
        }

        var carried = JewelBankInventory.Count(player);
        var counts = new byte[JewelBankKinds.Count * sizeof(uint)];
        var inventory = new byte[JewelBankKinds.Count * sizeof(uint)];
        for (var index = 0; index < JewelBankKinds.Count; index++)
        {
            var code = (byte)(index + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(counts.AsSpan(index * sizeof(uint)), (uint)Math.Clamp(state.CountOf(code), 0, uint.MaxValue));
            BinaryPrimitives.WriteUInt32LittleEndian(inventory.AsSpan(index * sizeof(uint)), (uint)Math.Clamp(carried.GetValueOrDefault(code), 0, uint.MaxValue));
        }

        await connection.SendJewelBankStateResponseAsync(ToWire(result), state.AutoDeposit ? (byte)1 : (byte)0, counts, inventory).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the amounts of a request (one little-endian 32-bit value per kind, in the order of the kind codes) into a dictionary; kinds with no amount are left out.
    /// </summary>
    /// <param name="amounts">The amounts of the kinds with code 1 to 45.</param>
    /// <returns>The amounts by kind code.</returns>
    public static Dictionary<byte, long> ToAmounts(ReadOnlySpan<byte> amounts)
    {
        var result = new Dictionary<byte, long>();
        for (var index = 0; index < JewelBankKinds.Count && (index + 1) * sizeof(uint) <= amounts.Length; index++)
        {
            var amount = BinaryPrimitives.ReadUInt32LittleEndian(amounts.Slice(index * sizeof(uint)));
            if (amount > 0)
            {
                result[(byte)(index + 1)] = amount;
            }
        }

        return result;
    }

    /// <summary>
    /// Sends the outcome of an operation to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The operation identifier of the request.</param>
    /// <param name="operation">The kind of operation.</param>
    /// <param name="result">The outcome.</param>
    /// <param name="units">The units which moved, summed over all kinds.</param>
    /// <returns>A task which completes when the response is sent.</returns>
    public static async ValueTask SendOperationAsync(Player player, byte[] operationId, OperationKind operation, JewelBankResultCode result, long units)
    {
        if (player is not RemotePlayer { Connection: { } connection })
        {
            return;
        }

        await connection.SendJewelBankOperationResponseAsync(operationId, (byte)operation, ToWire(result), 0, (uint)Math.Clamp(units, 0, uint.MaxValue)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a mutating request: checks the budget and availability, calls the operation, and sends the outcome and the new state.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="operationId">The operation identifier of the request.</param>
    /// <param name="operation">The kind of operation.</param>
    /// <param name="run">The operation, which receives the service and the client-made operation identifier.</param>
    /// <returns>A task which completes when the response is sent.</returns>
    public static async ValueTask RunAsync(Player player, byte[] operationId, OperationKind operation, Func<JewelBankService, Guid, Task<JewelBankOperationResult>> run)
    {
        var service = JewelBankService.Create(player);
        if (JewelBankService.GetIdentity(player) is not { } who)
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.InvalidRequest, 0).ConfigureAwait(false);
            return;
        }

        if (service is null)
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.ServiceUnavailable, 0).ConfigureAwait(false);
            return;
        }

        if (!JewelBankService.RateLimiter.TryAcquire(who.AccountId))
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.RateLimited, 0).ConfigureAwait(false);
            return;
        }

        var result = await run(service, new Guid(operationId)).ConfigureAwait(false);
        await SendOperationAsync(player, operationId, operation, result.Result, result.Moved.Values.Sum()).ConfigureAwait(false);
        var state = result.State ?? await service.GetStateAsync(who.AccountId).ConfigureAwait(false);
        await SendStateAsync(player, JewelBankResultCode.Success, state).ConfigureAwait(false);
    }

    private static JewelBankResult ToWire(JewelBankResultCode result) => (JewelBankResult)(byte)result;
}
