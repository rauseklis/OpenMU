namespace MUnique.OpenMU.GameLogic.Premium;

using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Restores unexpired premium effects when a character enters the world.
/// </summary>
[PlugIn]
[Guid("60B9BC89-6A39-43B0-8F43-B778BEA9A7D4")]
public sealed class RestorePremiumBuffsPlugIn : IPlayerStateChangedPlugIn
{
    public ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState) =>
        previousState == PlayerState.CharacterSelection && currentState == PlayerState.EnteredWorld
            ? PremiumBuffService.RestoreAsync(player)
            : ValueTask.CompletedTask;
}
