// <copyright file="NpcReachTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.AntiCheat;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.MessageHandler;
using MUnique.OpenMU.Pathfinding;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>
/// Tests that an NPC window does not stay usable when the player is far away from the NPC.
/// </summary>
[TestFixture]
public class NpcReachTests
{
    private static readonly Point StartPoint = new(147, 120);

    private int _originalDistance;
    private AntiCheatMode _originalMode;

    /// <summary>Remembers the shared default configuration, which these tests change.</summary>
    [SetUp]
    public void SetUp()
    {
        this._originalDistance = AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance;
        this._originalMode = AntiCheatState.DefaultConfiguration.NpcReachMode;
    }

    /// <summary>Restores the shared default configuration.</summary>
    [TearDown]
    public void TearDown()
    {
        AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance = this._originalDistance;
        AntiCheatState.DefaultConfiguration.NpcReachMode = this._originalMode;
    }

    /// <summary>Walking away from the NPC closes its window, so that a shop, vault or machine can not be used from afar.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WalkingAwayClosesTheNpcWindowAsync()
    {
        AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance = 3;
        var (player, npc) = await CreateOpenedNpcAsync().ConfigureAwait(false);

        await WalkEastAsync(player).ConfigureAwait(false); // to (151, 122): four tiles away from the NPC

        Assert.That(player.OpenedNpc, Is.Null);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
    }

    /// <summary>Walking a short way next to the NPC leaves the window open.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task WalkingNearTheNpcKeepsTheWindowAsync()
    {
        AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance = 12;
        var (player, npc) = await CreateOpenedNpcAsync().ConfigureAwait(false);

        await WalkEastAsync(player).ConfigureAwait(false);

        Assert.That(player.OpenedNpc, Is.SameAs(npc));
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.NpcDialogOpened));
    }

    /// <summary>In shadow mode nothing is closed.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task ShadowModeClosesNothingAsync()
    {
        AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance = 3;
        AntiCheatState.DefaultConfiguration.NpcReachMode = AntiCheatMode.Shadow;
        var (player, npc) = await CreateOpenedNpcAsync().ConfigureAwait(false);

        await WalkEastAsync(player).ConfigureAwait(false);

        Assert.That(player.OpenedNpc, Is.SameAs(npc));
    }

    /// <summary>An NPC which is far away can not be talked to.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TalkingToAFarNpcIsRefusedAsync()
    {
        AntiCheatState.DefaultConfiguration.NpcReachMaximumDistance = 3;
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.PositionX = StartPoint.X;
        player.SelectedCharacter.PositionY = StartPoint.Y;
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld), "precondition");
        var npc = CreateNpc(new Point((byte)(StartPoint.X + 20), StartPoint.Y));

        await new TalkNpcAction().TalkToNpcAsync(player, npc).ConfigureAwait(false);

        Assert.That(player.OpenedNpc, Is.Null);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
    }

    /// <summary>The defaults close the window of a far NPC and are generous.</summary>
    [Test]
    public void DefaultsAreGenerousAndEnforced()
    {
        var configuration = new AntiCheatConfiguration();

        Assert.That(configuration.NpcReachMode, Is.EqualTo(AntiCheatMode.Enforce));
        Assert.That(configuration.NpcReachMaximumDistance, Is.GreaterThanOrEqualTo(8));
    }

    private static NonPlayerCharacter CreateNpc(Point position)
    {
        var definition = new MonsterDefinition { Id = Guid.NewGuid(), ObjectKind = NpcObjectKind.PassiveNpc };
        var npc = new Mock<NonPlayerCharacter>(null!, definition, null!) { CallBase = true };
        npc.Setup(n => n.Position).Returns(position);
        return npc.Object;
    }

    private static async Task<(Player Player, NonPlayerCharacter Npc)> CreateOpenedNpcAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.SelectedCharacter!.PositionX = StartPoint.X;
        player.SelectedCharacter.PositionY = StartPoint.Y;
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld), "precondition");
        Assert.That(await player.PlayerState.TryAdvanceToAsync(PlayerState.NpcDialogOpened).ConfigureAwait(false), Is.True, "precondition");
        var npc = CreateNpc(StartPoint);
        player.OpenedNpc = npc;
        return (player, npc);
    }

    private static async Task WalkEastAsync(Player player)
    {
        // By example: walking from 147, 120 to 151, 122: C1 08 D4 93 78 44 33 44
        var packet = new byte[] { 0xC1, 0x08, (byte)PacketType.Walk, 0x93, 0x78, 0x44, 0x33, 0x44 };
        await new CharacterWalkHandlerPlugIn().HandlePacketAsync(player, packet).ConfigureAwait(false);
    }
}
