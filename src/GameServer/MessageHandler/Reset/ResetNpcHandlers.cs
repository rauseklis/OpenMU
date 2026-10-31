// <copyright file="ResetNpcHandlers.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Reset;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Resets;
using MUnique.OpenMU.GameLogic.Views.Reset;
using MUnique.OpenMU.PlugIns;
using Wire = MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Packet handler for the packets of the reset NPC window, with the 0xDC identifier.
/// </summary>
[PlugIn]
[Guid("D2F4A6C8-0B1D-4E3F-8A59-7C6B5D4E3F21")]
internal class ResetNpcGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key.
    /// </summary>
    internal const byte GroupKey = 0xDC;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetNpcGroupHandlerPlugIn"/> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The plugin manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public ResetNpcGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}

/// <summary>
/// The shared parts of the handlers of the reset NPC window.
/// </summary>
internal static class ResetNpcRequests
{
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(500);
    private static readonly ConditionalWeakTable<Player, StrongBox<DateTime>> LastRequests = new();

    /// <summary>
    /// Determines whether the player has the reset NPC open (it is the NPC the player talked to) and does not ask too often.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns><c>true</c> when the request may be answered.</returns>
    public static bool IsAllowed(Player player)
    {
        if (player.OpenedNpc?.Definition.Number != ResetCharacterNpcPlugin.ResetNpcNumber)
        {
            return false;
        }

        var last = LastRequests.GetValue(player, _ => new StrongBox<DateTime>(DateTime.MinValue));
        var now = DateTime.UtcNow;
        lock (last)
        {
            if (now - last.Value < MinimumRequestInterval)
            {
                return false;
            }

            last.Value = now;
            return true;
        }
    }

    /// <summary>
    /// Sends a status to the window.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="status">The status.</param>
    /// <returns>A task which completes when the status is sent.</returns>
    public static ValueTask SendAsync(Player player, ResetNpcStatus status)
        => player.InvokeViewPlugInAsync<IShowResetNpcPlugIn>(p => p.ShowResetNpcAsync(status));

    /// <summary>
    /// Sends a refusal without any numbers.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>A task which completes when the answer is sent.</returns>
    public static ValueTask SendRefusedAsync(Player player)
        => SendAsync(player, new ResetNpcStatus(ResetNpcOutcome.Refused, 0, 0, 0, 0, 0, 0, 0, 0));
}

/// <summary>
/// Handles the request for the current requirements and values (the window refreshes them).
/// </summary>
[PlugIn]
[Guid("A4B6C8D0-1E2F-4A3B-9C5D-6E7F8091A2B3")]
[BelongsToGroup(ResetNpcGroupHandlerPlugIn.GroupKey)]
internal sealed class ResetNpcInfoHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.ResetNpcInfoRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.ResetNpcInfoRequest.Length)
        {
            return;
        }

        try
        {
            if (!ResetNpcRequests.IsAllowed(player)
                || player.OpenedNpc is not { } npc
                || await new ResetCharacterAction(player, npc).GetStatusAsync().ConfigureAwait(false) is not { } status)
            {
                await ResetNpcRequests.SendRefusedAsync(player).ConfigureAwait(false);
                return;
            }

            await ResetNpcRequests.SendAsync(player, status).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The window waits for an answer, so it always gets one.
            player.Logger.LogError(ex, "The reset information for {Player} could not be answered.", player);
            await ResetNpcRequests.SendRefusedAsync(player).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Handles the press of the Reset button: the costs are checked again, taken, and the character is reset.
/// </summary>
[PlugIn]
[Guid("B5C7D9E1-2F30-4B4C-AD6E-7F8091A2B3C4")]
[BelongsToGroup(ResetNpcGroupHandlerPlugIn.GroupKey)]
internal sealed class ResetNpcResetHandlerPlugIn : ISubPacketHandlerPlugIn
{
    /// <inheritdoc />
    public bool IsEncryptionExpected => true;

    /// <inheritdoc />
    public byte Key => Wire.ResetNpcResetRequest.SubCode;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < Wire.ResetNpcResetRequest.Length)
        {
            return;
        }

        if (!ResetNpcRequests.IsAllowed(player) || player.OpenedNpc is not { } npc)
        {
            await ResetNpcRequests.SendRefusedAsync(player).ConfigureAwait(false);
            return;
        }

        try
        {
            // The reset may log the player out, so the client is told before that happens.
            var action = new ResetCharacterAction(
                player,
                npc,
                () => ResetNpcRequests.SendAsync(player, new ResetNpcStatus(ResetNpcOutcome.Done, 0, 0, 0, 0, 0, 0, 0, 0)));
            var outcome = await action.ResetCharacterAsync().ConfigureAwait(false);
            if (outcome == ResetNpcOutcome.Done)
            {
                return;
            }

            var status = await action.GetStatusAsync().ConfigureAwait(false);
            await ResetNpcRequests.SendAsync(player, status?.WithOutcome(outcome) ?? new ResetNpcStatus(outcome, 0, 0, 0, 0, 0, 0, 0, 0)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The window waits for an answer, so it always gets one, even when something went wrong.
            player.Logger.LogError(ex, "The reset of {Player} failed unexpectedly.", player);
            await ResetNpcRequests.SendRefusedAsync(player).ConfigureAwait(false);
        }
    }
}
