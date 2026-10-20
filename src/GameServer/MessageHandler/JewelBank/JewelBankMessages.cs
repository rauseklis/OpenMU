// <copyright file="JewelBankMessages.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.JewelBank;

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

        await connection.SendJewelBankStateResponseAsync(
            ToWire(result),
            state.AutoDeposit ? (byte)1 : (byte)0,
            Count(state, 1), Count(state, 2), Count(state, 3), Count(state, 4), Count(state, 5),
            Count(state, 6), Count(state, 7), Count(state, 8), Count(state, 9), Count(state, 10),
            Count(state, 11), Count(state, 12), Count(state, 13), Count(state, 14), Count(state, 15)).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the outcome of an operation to the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="operationId">The operation identifier of the request.</param>
    /// <param name="operation">The kind of operation.</param>
    /// <param name="result">The outcome.</param>
    /// <param name="kindCode">The kind which was asked for (0 for every kind).</param>
    /// <param name="units">The units which moved.</param>
    /// <returns>A task which completes when the response is sent.</returns>
    public static async ValueTask SendOperationAsync(Player player, byte[] operationId, OperationKind operation, JewelBankResultCode result, byte kindCode, long units)
    {
        if (player is not RemotePlayer { Connection: { } connection })
        {
            return;
        }

        await connection.SendJewelBankOperationResponseAsync(operationId, (byte)operation, ToWire(result), kindCode, (uint)Math.Clamp(units, 0, uint.MaxValue)).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a mutating request: checks the budget and availability, calls the operation, and sends the outcome and the new state.
    /// </summary>
    /// <param name="player">The requesting player.</param>
    /// <param name="operationId">The operation identifier of the request.</param>
    /// <param name="operation">The kind of operation.</param>
    /// <param name="kindCode">The kind which was asked for.</param>
    /// <param name="run">The operation, which receives the service and the client-made operation identifier.</param>
    /// <returns>A task which completes when the response is sent.</returns>
    public static async ValueTask RunAsync(Player player, byte[] operationId, OperationKind operation, byte kindCode, Func<JewelBankService, Guid, Task<JewelBankOperationResult>> run)
    {
        var service = JewelBankService.Create(player);
        if (JewelBankService.GetIdentity(player) is not { } who)
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.InvalidRequest, kindCode, 0).ConfigureAwait(false);
            return;
        }

        if (service is null)
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.ServiceUnavailable, kindCode, 0).ConfigureAwait(false);
            return;
        }

        if (!JewelBankService.RateLimiter.TryAcquire(who.AccountId))
        {
            await SendOperationAsync(player, operationId, operation, JewelBankResultCode.RateLimited, kindCode, 0).ConfigureAwait(false);
            return;
        }

        var result = await run(service, new Guid(operationId)).ConfigureAwait(false);
        await SendOperationAsync(player, operationId, operation, result.Result, kindCode, result.Moved.Values.Sum()).ConfigureAwait(false);
        var state = result.State ?? await service.GetStateAsync(who.AccountId).ConfigureAwait(false);
        await SendStateAsync(player, result.Result, state).ConfigureAwait(false);
    }

    private static ulong Count(JewelBankState state, byte code) => (ulong)Math.Max(0, state.CountOf(code));

    private static JewelBankResult ToWire(JewelBankResultCode result) => (JewelBankResult)(byte)result;
}
