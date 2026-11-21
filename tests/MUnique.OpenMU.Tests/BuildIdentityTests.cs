// <copyright file="BuildIdentityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// Tests the observation of the client build identifier at login. It only observes: nothing is ever refused.
/// </summary>
[TestFixture]
public class BuildIdentityTests
{
    /// <summary>Nothing is compared while no allowed builds are configured.</summary>
    [Test]
    public void EmptyAllowListComparesNothing()
    {
        var configuration = new AntiCheatConfiguration();

        Assert.That(BuildIdentity.Check(configuration, "anything"), Is.EqualTo(BuildCheckResult.NotChecked));
    }

    /// <summary>A serial in the list is known, any other is unknown.</summary>
    [Test]
    public void SerialIsComparedWithTheAllowList()
    {
        var configuration = new AntiCheatConfiguration { AllowedBuildSerials = { "BuildSerial000001" } };

        Assert.That(BuildIdentity.Check(configuration, "BuildSerial000001"), Is.EqualTo(BuildCheckResult.Known));
        Assert.That(BuildIdentity.Check(configuration, "SomethingElse0000"), Is.EqualTo(BuildCheckResult.Unknown));
        Assert.That(BuildIdentity.Check(configuration, "buildserial000001"), Is.EqualTo(BuildCheckResult.Unknown), "the comparison is case sensitive");
    }

    /// <summary>When the check is off, nothing is compared or logged.</summary>
    [Test]
    public void OffModeObservesNothing()
    {
        var configuration = new AntiCheatConfiguration { BuildMode = AntiCheatMode.Off, AllowedBuildSerials = { "A" } };
        var logger = new Mock<ILogger>();

        var result = BuildIdentity.Observe(configuration, logger.Object, "B-off", "0.97", "1.2.3.4");

        Assert.That(result, Is.EqualTo(BuildCheckResult.NotChecked));
        logger.Verify(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
    }

    /// <summary>An unknown build is reported, also in enforce mode, but never refused (the call only returns the result), and each distinct build is logged once.</summary>
    [Test]
    public void UnknownBuildIsLoggedOnceAndNeverRefused()
    {
        var configuration = new AntiCheatConfiguration { BuildMode = AntiCheatMode.Enforce, AllowedBuildSerials = { "KnownSerial0000001" } };
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var serial = "Unknown-" + Guid.NewGuid().ToString("N");

        var first = BuildIdentity.Observe(configuration, logger.Object, serial, "0.97", "1.2.3.4");
        var second = BuildIdentity.Observe(configuration, logger.Object, serial, "0.97", "5.6.7.8");

        Assert.That(first, Is.EqualTo(BuildCheckResult.Unknown));
        Assert.That(second, Is.EqualTo(BuildCheckResult.Unknown));
        logger.Verify(l => l.Log(LogLevel.Information, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("rule=build.seen") && v.ToString()!.Contains("result=Unknown")), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    /// <summary>Control characters of a hostile serial never reach the log.</summary>
    [Test]
    public void LoggedSerialIsPrintableAndShort()
    {
        var configuration = new AntiCheatConfiguration();
        var messages = new List<string>();
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i => messages.Add(i.Arguments[2].ToString()!)));

        BuildIdentity.Observe(configuration, logger.Object, "line\nbreak\u0001" + new string('x', 100) + Guid.NewGuid().ToString("N"), "0.97", "1.2.3.4");

        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0], Does.Not.Contain("\n"));
        Assert.That(messages[0].Length, Is.LessThan(300));
    }
}
