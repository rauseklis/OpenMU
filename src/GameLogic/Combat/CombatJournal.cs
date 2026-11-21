// <copyright file="CombatJournal.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Combat;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// An append-only journal file with one JSON line per combat event. Each line contains the hash of the previous
/// line, so a change or removal of an earlier line can be detected by whoever reads the file.
/// </summary>
internal sealed class CombatJournal : IDisposable
{
    internal const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    private static readonly JsonSerializerOptions EventOptions = new() { Converters = { new JsonStringEnumConverter() } };

    private readonly object _sync = new();
    private readonly string _path;
    private FileStream? _stream;
    private long _sequence;
    private string _lastHash = Genesis;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatJournal"/> class and continues the chain of an existing file.
    /// </summary>
    /// <param name="path">The file path.</param>
    public CombatJournal(string path)
    {
        this._path = path;
        if (File.Exists(path) && File.ReadLines(path).LastOrDefault(l => l.Length > 0) is { } last)
        {
            var entry = JsonSerializer.Deserialize<JournalLine>(last) ?? throw new InvalidDataException("The journal file is unreadable.");
            this._sequence = entry.Sequence;
            this._lastHash = entry.Hash;
        }
    }

    /// <summary>
    /// Computes the hash of a journal line.
    /// </summary>
    /// <param name="previousHash">The hash of the previous line.</param>
    /// <param name="sequence">The sequence number.</param>
    /// <param name="time">The time.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="json">The event as JSON.</param>
    /// <returns>The hash as lower case hexadecimal text.</returns>
    internal static string ComputeHash(string previousHash, long sequence, DateTime time, string kind, string json)
    {
        var data = $"{previousHash}|{sequence}|{time.ToUniversalTime():O}|{kind}|{json}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    /// <summary>
    /// Appends an event and flushes it to disk. Throws if that did not work.
    /// </summary>
    /// <param name="combatEvent">The event.</param>
    public void Append(CombatEvent combatEvent)
    {
        lock (this._sync)
        {
            this._stream ??= new FileStream(this._path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            var json = JsonSerializer.Serialize(combatEvent, EventOptions);
            var kind = combatEvent.Kind.ToString();
            var sequence = this._sequence + 1;
            var time = DateTime.UtcNow;
            var hash = ComputeHash(this._lastHash, sequence, time, kind, json);
            var line = JsonSerializer.Serialize(new JournalLine(sequence, time, kind, json, this._lastHash, hash)) + "\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            try
            {
                this._stream.Write(bytes, 0, bytes.Length);
                this._stream.Flush(true);
            }
            catch
            {
                this._stream.Dispose();
                this._stream = null;
                throw;
            }

            this._sequence = sequence;
            this._lastHash = hash;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this._sync)
        {
            this._stream?.Dispose();
            this._stream = null;
        }
    }

    /// <summary>
    /// One line of the journal file.
    /// </summary>
    /// <param name="Sequence">The sequence number, starting at one.</param>
    /// <param name="UtcTime">The time of the entry.</param>
    /// <param name="Kind">The kind of the event.</param>
    /// <param name="Json">The event as JSON.</param>
    /// <param name="PrevHash">The hash of the previous line.</param>
    /// <param name="Hash">The hash of this line.</param>
    internal sealed record JournalLine(long Sequence, DateTime UtcTime, string Kind, string Json, string PrevHash, string Hash);
}
