// <copyright file="NpcWindowOnWarpTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests that the window of an NPC does not survive a warp: the old NPC can not be used from the new place.
/// </summary>
[TestFixture]
public class NpcWindowOnWarpTests
{
    /// <summary>Warping away clears the opened NPC and the vault which belonged to it.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WarpClearsTheOpenedNpcAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false), Is.True, "precondition");
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.PassiveNpc };
        player.OpenedNpc = new NonPlayerCharacter(null!, definition, null!);
        var gate = new ExitGate { Map = player.CurrentMap!.Definition, X1 = 150, X2 = 150, Y1 = 120, Y2 = 120 };

        await player.WarpToAsync(gate).ConfigureAwait(false);

        Assert.That(player.OpenedNpc, Is.Null, "the old NPC can not be used from the new place");
        Assert.That(player.Vault, Is.Null);
    }
}
