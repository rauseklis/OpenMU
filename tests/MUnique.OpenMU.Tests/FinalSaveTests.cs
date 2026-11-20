// <copyright file="FinalSaveTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests the save which runs when a player leaves the game: it is retried, and a save which keeps failing is counted.
/// </summary>
[TestFixture]
public class FinalSaveTests
{
    private static readonly TimeSpan[] NoDelay = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero];

    [SetUp]
    public void ResetHealth() => FinalSaveHealth.Reset();

    /// <summary>A save which works is run once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task SuccessfulSaveRunsOnceAsync()
    {
        var calls = 0;

        var saved = await FinalSave.RunAsync(() => { calls++; return ValueTask.FromResult(true); }, NullLogger.Instance, "p", NoDelay).ConfigureAwait(false);

        Assert.That(saved, Is.True);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(FinalSaveHealth.FailureCount, Is.EqualTo(0));
    }

    /// <summary>A save which fails with an exception and with a false result, then works, is retried until it works.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task TransientFailuresAreRetriedAsync()
    {
        var calls = 0;

        var saved = await FinalSave.RunAsync(
            () =>
            {
                calls++;
                return calls switch
                {
                    1 => throw new InvalidOperationException("database is busy"),
                    2 => ValueTask.FromResult(false),
                    _ => ValueTask.FromResult(true),
                };
            },
            NullLogger.Instance,
            "p",
            NoDelay).ConfigureAwait(false);

        Assert.That(saved, Is.True);
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(FinalSaveHealth.FailureCount, Is.EqualTo(0), "a save which worked in the end is no failure");
    }

    /// <summary>A save which keeps failing returns false after all attempts and is counted once.</summary>
    /// <returns>The task.</returns>
    [Test]
    public async Task PersistentFailureIsCountedAsync()
    {
        var calls = 0;

        var saved = await FinalSave.RunAsync(() => { calls++; throw new InvalidOperationException("database is down"); }, NullLogger.Instance, "Hero", NoDelay).ConfigureAwait(false);

        Assert.That(saved, Is.False);
        Assert.That(calls, Is.EqualTo(NoDelay.Length + 1));
        Assert.That(FinalSaveHealth.FailureCount, Is.EqualTo(1));
        Assert.That(FinalSaveHealth.LastFailedSubject, Is.EqualTo("Hero"));
        Assert.That(FinalSaveHealth.LastFailureUtc, Is.Not.Null);
    }
}
