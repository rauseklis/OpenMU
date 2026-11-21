// <copyright file="CombatJournalConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

/// <summary>
/// The configuration of the <see cref="CombatJournalPlugIn"/>. The journal is off until it is switched on and given a folder.
/// </summary>
public class CombatJournalConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the journal is written.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the folder of the journal file. Nothing is written while it is empty.
    /// </summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size in megabytes after which the journal file is closed and a new one is started (the hash chain
    /// continues in the new file). Zero keeps one file for ever.
    /// </summary>
    public int MaximumFileMegabytes { get; set; } = 100;
}
