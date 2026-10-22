// <copyright file="PlayerCharacterExtensionsTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests which characters of an account belong to the game server of a player.
/// </summary>
[TestFixture]
public class PlayerCharacterExtensionsTest
{
    /// <summary>
    /// Tests that only the characters of the game server of the player are listed.
    /// </summary>
    [Test]
    public async ValueTask OnlyCharactersOfThisServerAreListedAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var characters = new List<Character>();
        Mock.Get(player.Account!).Setup(a => a.Characters).Returns(characters);
        characters.Add(CreateCharacter("OnServer0", 0));
        characters.Add(CreateCharacter("OnServer1", 1));

        var listed = player.GetCharactersOfThisServer().Select(c => c.Name).ToList();

        Assert.That(player.GetGameServerId(), Is.EqualTo(0));
        Assert.That(listed, Is.EquivalentTo(new[] { "OnServer0" }));
    }

    private static Character CreateCharacter(string name, byte serverId)
    {
        var character = new Mock<Character>();
        character.SetupAllProperties();
        character.Object.Name = name;
        character.Object.GameServerId = serverId;
        return character.Object;
    }
}
