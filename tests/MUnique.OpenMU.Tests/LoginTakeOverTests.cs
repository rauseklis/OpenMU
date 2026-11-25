// <copyright file="LoginTakeOverTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic.PlayerActions;

/// <summary>
/// Tests the counting of failed logins ("account already connected"). The third failed attempt takes over the account.
/// </summary>
[TestFixture]
public class LoginTakeOverTests
{
    /// <summary>
    /// Tests that only the third failed attempt in a row takes over, and that counting starts again afterwards.
    /// </summary>
    [Test]
    public void ThirdFailedAttemptTakesOver()
    {
        var name = Guid.NewGuid().ToString();

        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.False, "1st");
        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.False, "2nd");
        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.True, "3rd");
        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.False, "counting starts again");
        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.False, "2nd of the new round");
        Assert.That(LoginAction.RegisterFailedAttempt(name), Is.True, "3rd of the new round");
    }

    /// <summary>
    /// Tests that different accounts are counted separately.
    /// </summary>
    [Test]
    public void AccountsAreCountedSeparately()
    {
        var first = Guid.NewGuid().ToString();
        var second = Guid.NewGuid().ToString();

        Assert.That(LoginAction.RegisterFailedAttempt(first), Is.False);
        Assert.That(LoginAction.RegisterFailedAttempt(second), Is.False);
        Assert.That(LoginAction.RegisterFailedAttempt(first), Is.False);
        Assert.That(LoginAction.RegisterFailedAttempt(second), Is.False);
        Assert.That(LoginAction.RegisterFailedAttempt(first), Is.True);
        Assert.That(LoginAction.RegisterFailedAttempt(second), Is.True);
    }
}
