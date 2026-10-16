namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameServer.RemoteView.World;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Refreshes the owner's currently applied effects when a tooltip is inspected.
/// </summary>
[PlugIn]
[Guid("FF8A9F42-8210-485B-A74E-17D620E6E817")]
internal sealed class MagicEffectDetailsRequestHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly ConditionalWeakTable<Player, RequestState> _requests = new();

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public byte Key => MagicEffectDetailsPacket.Code;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length != 4 || packet.Span[3] is not (1 or MagicEffectDetailsPacket.Version) || player is not RemotePlayer remote || player.Attributes is null)
        {
            return;
        }

        var state = this._requests.GetValue(player, _ => new RequestState());
        var now = Stopwatch.GetTimestamp();
        lock (state)
        {
            if (state.LastRequest != 0 && Stopwatch.GetElapsedTime(state.LastRequest, now) < TimeSpan.FromMilliseconds(500))
            {
                return;
            }

            state.LastRequest = now;
        }
        await MagicEffectDetailsPacket.ClearAsync(remote).ConfigureAwait(false);
        foreach (var effect in player.MagicEffectList.GetActiveEffectsSnapshot())
        {
            if (!effect.IsDisposed && !effect.IsDisposing)
            {
                await MagicEffectDetailsPacket.SendAsync(remote, effect, true).ConfigureAwait(false);
            }
        }
    }

    private sealed class RequestState
    {
        public long LastRequest { get; set; }
    }
}
