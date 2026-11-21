// <copyright file="CombatJournalPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Combat;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Writes the combat events (applied hits, ended lives, experience settlements) into an append-only journal file
/// which a separate process can read. Off by default.
/// </summary>
[PlugIn]
[Display(Name = "Combat event journal", Description = "Writes resolved hits, ended lives and experience settlements into a hash-chained journal file. Off until enabled and given a folder.")]
[Guid("3C8D5E21-9A47-4B16-8F02-6D1E7B4A9C53")]
public sealed class CombatJournalPlugIn : IFeaturePlugIn, ISupportCustomConfiguration<CombatJournalConfiguration>, ISupportDefaultCustomConfiguration, ICombatEventPlugIn, ICustodyEventPlugIn, IDisposable
{
    private readonly object _sync = new();
    private CombatJournal? _journal;
    private string? _journalPath;

    /// <inheritdoc />
    public CombatJournalConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public object CreateDefaultConfig() => new CombatJournalConfiguration();

    /// <inheritdoc />
    public void OnCombatEvent(CombatEvent combatEvent)
    {
        var configuration = this.Configuration;
        if (configuration is not { Enabled: true } || string.IsNullOrWhiteSpace(configuration.Directory))
        {
            return;
        }

        try
        {
            this.GetJournal(configuration.Directory).Append(combatEvent);
        }
        catch (Exception ex)
        {
            // The game goes on; the failure is visible to the operator.
            CombatEventHealth.RecordFailure(ex);
        }
    }

    /// <inheritdoc />
    public void OnCustodyEvent(CustodyEvent custodyEvent)
    {
        var configuration = this.Configuration;
        if (configuration is not { Enabled: true } || string.IsNullOrWhiteSpace(configuration.Directory))
        {
            return;
        }

        try
        {
            this.GetJournal(configuration.Directory).Append(custodyEvent);
        }
        catch (Exception ex)
        {
            CombatEventHealth.RecordFailure(ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this._sync)
        {
            this._journal?.Dispose();
            this._journal = null;
        }
    }

    private CombatJournal GetJournal(string directory)
    {
        lock (this._sync)
        {
            var path = Path.Combine(directory, "combat-journal.jsonl");
            if (this._journal is null || this._journalPath != path)
            {
                this._journal?.Dispose();
                this._journal = new CombatJournal(path, (long)Math.Max(0, this.Configuration?.MaximumFileMegabytes ?? 0) * 1024 * 1024);
                this._journalPath = path;
            }

            return this._journal;
        }
    }
}
