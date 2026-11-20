// <copyright file="FriendServerChatRoomTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.FriendServer;
using MUnique.OpenMU.Persistence.InMemory;
using FriendServerImplementation = MUnique.OpenMU.FriendServer.FriendServer;

/// <summary>
/// Tests for the invitations to chat rooms of the friend server.
/// </summary>
[TestFixture]
public sealed class FriendServerChatRoomTest
{
    private const ushort RoomId = 7;

    private Mock<IChatServer> _chatServer = null!;
    private IFriendServer _friendServer = null!;
    private Character _creator = null!;
    private Character _guest = null!;
    private Character _outsider = null!;
    private Character _invitee = null!;

    /// <summary>
    /// Sets up four online players: <c>creator</c> and <c>guest</c> are in a chat room; <c>outsider</c> is a friend of
    /// <c>guest</c>, but not part of the room; <c>invitee</c> is a friend of <c>creator</c> and of <c>outsider</c>.
    /// </summary>
    /// <returns>The task.</returns>
    [SetUp]
    public async Task SetUpAsync()
    {
        this._chatServer = new Mock<IChatServer>();
        this._chatServer.Setup(c => c.CreateChatRoomAsync()).ReturnsAsync(RoomId);
        this._chatServer
            .Setup(c => c.RegisterClientAsync(It.IsAny<ushort>(), It.IsAny<string>()))
            .ReturnsAsync((ushort room, string name) => new ChatServerAuthenticationInfo(0, room, name, "127.0.0.1", "token"));

        var persistence = new InMemoryPersistenceContextProvider();
        this._friendServer = new FriendServerImplementation(new Mock<IFriendNotifier>().Object, this._chatServer.Object, persistence, NullLogger<FriendServerImplementation>.Instance);

        var context = persistence.CreateNewContext();
        this._creator = this.CreateCharacter(context, "creator");
        this._guest = this.CreateCharacter(context, "guest");
        this._outsider = this.CreateCharacter(context, "outsider");
        this._invitee = this.CreateCharacter(context, "invitee");

        foreach (var character in new[] { this._creator, this._guest, this._outsider, this._invitee })
        {
            await this._friendServer.PlayerEnteredGameAsync(1, character.Id, character.Name).ConfigureAwait(false);
        }

        await this.BefriendAsync(this._creator, this._guest).ConfigureAwait(false);
        await this.BefriendAsync(this._outsider, this._guest).ConfigureAwait(false);
        await this.BefriendAsync(this._creator, this._invitee).ConfigureAwait(false);
        await this.BefriendAsync(this._outsider, this._invitee).ConfigureAwait(false);

        await this._friendServer.CreateChatRoomAsync(this._creator.Name, this._guest.Name).ConfigureAwait(false);
        this._chatServer.Invocations.Clear();
    }

    /// <summary>
    /// A member of the room can invite a friend.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task MemberCanInviteFriendAsync()
    {
        var invited = await this._friendServer.InviteFriendToChatRoomAsync(this._creator.Name, this._invitee.Name, RoomId).ConfigureAwait(false);

        Assert.That(invited, Is.True);
        this._chatServer.Verify(c => c.RegisterClientAsync(RoomId, this._invitee.Name), Times.Once);
    }

    /// <summary>
    /// A player who is not part of the room can not put their friends into it.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task NonMemberCannotInviteAsync()
    {
        var invited = await this._friendServer.InviteFriendToChatRoomAsync(this._outsider.Name, this._guest.Name, RoomId).ConfigureAwait(false);

        Assert.That(invited, Is.False);
        this._chatServer.Verify(c => c.RegisterClientAsync(It.IsAny<ushort>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A room number which does not exist is refused without asking the chat server.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task UnknownRoomIsRefusedAsync()
    {
        var invited = await this._friendServer.InviteFriendToChatRoomAsync(this._creator.Name, this._invitee.Name, 4242).ConfigureAwait(false);

        Assert.That(invited, Is.False);
        this._chatServer.Verify(c => c.RegisterClientAsync(It.IsAny<ushort>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// Somebody who was invited into the room may invite others.
    /// </summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task InvitedPlayerBecomesMemberAsync()
    {
        await this._friendServer.InviteFriendToChatRoomAsync(this._creator.Name, this._invitee.Name, RoomId).ConfigureAwait(false);
        this._chatServer.Invocations.Clear();

        var invited = await this._friendServer.InviteFriendToChatRoomAsync(this._invitee.Name, this._outsider.Name, RoomId).ConfigureAwait(false);

        Assert.That(invited, Is.True);
    }

    private Character CreateCharacter(Persistence.IContext context, string name)
    {
        var character = context.CreateNew<Character>();
        character.Name = name;
        return character;
    }

    private async Task BefriendAsync(Character requester, Character friend)
    {
        await this._friendServer.FriendRequestAsync(requester.Name, friend.Name).ConfigureAwait(false);
        await this._friendServer.FriendResponseAsync(friend.Name, requester.Name, true).ConfigureAwait(false);
    }
}
