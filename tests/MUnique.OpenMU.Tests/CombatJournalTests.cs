// <copyright file="CombatJournalTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// Tests the combat event journal file.
/// </summary>
[TestFixture]
public class CombatJournalTests
{
    private string _directory = string.Empty;

    /// <summary>Creates a temporary folder.</summary>
    [SetUp]
    public void SetUp()
    {
        this._directory = Path.Combine(Path.GetTempPath(), "journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._directory);
        CombatEventHealth.Reset();
    }

    /// <summary>Deletes the temporary folder.</summary>
    [TearDown]
    public void TearDown() => Directory.Delete(this._directory, true);

    private string JournalFile => Path.Combine(this._directory, "combat-journal.jsonl");

    /// <summary>Events are written as a chain whose hashes can be recomputed by a reader.</summary>
    [Test]
    public void EventsAreWrittenAsAVerifiableChain()
    {
        using var plugIn = Enabled(this._directory);

        plugIn.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));
        plugIn.OnCombatEvent(NewEvent(CombatEventKind.LifeEnded));
        plugIn.OnCombatEvent(NewEvent(CombatEventKind.ExperienceSettled));
        plugIn.Dispose();

        var lines = ReadLines(this.JournalFile);
        Assert.That(lines.Select(l => l.Sequence), Is.EqualTo(new long[] { 1, 2, 3 }));
        Assert.That(lines.Select(l => l.Kind), Is.EqualTo(new[] { "HitApplied", "LifeEnded", "ExperienceSettled" }));
        AssertChain(lines);
        Assert.That(lines[0].Json, Does.Contain("\"Kind\":\"HitApplied\""));
        Assert.That(CombatEventHealth.IsDegraded, Is.False);
    }

    /// <summary>Nothing is written while the journal is disabled or has no folder.</summary>
    [Test]
    public void DisabledJournalWritesNothing()
    {
        using var off = new CombatJournalPlugIn { Configuration = new CombatJournalConfiguration { Enabled = false, Directory = this._directory } };
        using var noFolder = new CombatJournalPlugIn { Configuration = new CombatJournalConfiguration { Enabled = true } };
        using var unconfigured = new CombatJournalPlugIn();

        off.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));
        noFolder.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));
        unconfigured.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));

        Assert.That(File.Exists(this.JournalFile), Is.False);
        Assert.That(CombatEventHealth.IsDegraded, Is.False);
    }

    /// <summary>After a restart the chain continues where it stopped.</summary>
    [Test]
    public void ChainContinuesAfterARestart()
    {
        using (var first = Enabled(this._directory))
        {
            first.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));
            first.OnCombatEvent(NewEvent(CombatEventKind.HitApplied));
        }

        using (var second = Enabled(this._directory))
        {
            second.OnCombatEvent(NewEvent(CombatEventKind.LifeEnded));
        }

        var lines = ReadLines(this.JournalFile);
        Assert.That(lines.Select(l => l.Sequence), Is.EqualTo(new long[] { 1, 2, 3 }));
        AssertChain(lines);
    }

    /// <summary>A journal which can not be written never throws into the game and makes the failure visible.</summary>
    [Test]
    public void UnwritableFolderIsReportedAndDoesNotThrow()
    {
        using var plugIn = Enabled(Path.Combine(this._directory, "missing", "folder"));

        Assert.DoesNotThrow(() => plugIn.OnCombatEvent(NewEvent(CombatEventKind.HitApplied)));

        Assert.That(CombatEventHealth.IsDegraded, Is.True);
        Assert.That(CombatEventHealth.FailureCount, Is.EqualTo(1));
    }

    private static CombatJournalPlugIn Enabled(string directory)
        => new() { Configuration = new CombatJournalConfiguration { Enabled = true, Directory = directory } };

    private static CombatEvent NewEvent(CombatEventKind kind)
        => new(kind, DateTime.UtcNow, new AttackKey(7, 0), 3, 11, 12, "Hero", Guid.NewGuid(), CombatController.Manual, 200, 100, 100, 0, 0);

    private static List<CombatJournal.JournalLine> ReadLines(string path)
        => File.ReadAllLines(path).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<CombatJournal.JournalLine>(l)!).ToList();

    private static void AssertChain(List<CombatJournal.JournalLine> lines)
    {
        var previous = CombatJournal.Genesis;
        foreach (var line in lines)
        {
            Assert.That(line.PrevHash, Is.EqualTo(previous));
            Assert.That(line.Hash, Is.EqualTo(CombatJournal.ComputeHash(previous, line.Sequence, line.UtcTime, line.Kind, line.Json)));
            previous = line.Hash;
        }
    }
}
