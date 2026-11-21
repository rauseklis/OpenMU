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
    private readonly long _maximumFileBytes;
    private FileStream? _stream;
    private long _sequence;
    private long _firstSequenceOfFile = 1;
    private string _lastHash = Genesis;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatJournal"/> class and continues the chain of an existing file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="maximumFileBytes">The size after which the file is closed and a new one is started; zero for no limit.</param>
    public CombatJournal(string path, long maximumFileBytes = 0)
    {
        this._path = path;
        this._maximumFileBytes = maximumFileBytes;
        if (File.Exists(path) && File.ReadLines(path).Where(l => l.Length > 0).ToList() is { Count: > 0 } lines)
        {
            this._firstSequenceOfFile = Parse(lines[0]).Sequence;
            var last = Parse(lines[^1]);
            this._sequence = last.Sequence;
            this._lastHash = last.Hash;
        }
        else if (ArchivedFiles(path).LastOrDefault() is { } newestArchive
                 && File.ReadLines(newestArchive).LastOrDefault(l => l.Length > 0) is { } archivedLast)
        {
            // The current file was just rotated away: the chain continues after the newest archived file.
            var last = Parse(archivedLast);
            this._sequence = last.Sequence;
            this._lastHash = last.Hash;
            this._firstSequenceOfFile = last.Sequence + 1;
        }
    }

    /// <summary>
    /// Gets the archived (rotated) files which belong to the journal file, oldest first.
    /// </summary>
    /// <param name="path">The path of the current journal file.</param>
    /// <returns>The paths of the archived files.</returns>
    internal static IEnumerable<string> ArchivedFiles(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, name + ".*" + extension).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    private static JournalLine Parse(string line)
        => JsonSerializer.Deserialize<JournalLine>(line) ?? throw new InvalidDataException("The journal file is unreadable.");

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
    public void Append(CombatEvent combatEvent) => this.Append(combatEvent.Kind.ToString(), JsonSerializer.Serialize(combatEvent, EventOptions));

    /// <summary>
    /// Appends an item custody event and flushes it to disk. Throws if that did not work.
    /// </summary>
    /// <param name="custodyEvent">The event.</param>
    public void Append(CustodyEvent custodyEvent) => this.Append(custodyEvent.Kind.ToString(), JsonSerializer.Serialize(custodyEvent, EventOptions));

    /// <summary>
    /// Appends a session event and flushes it to disk. Throws if that did not work.
    /// </summary>
    /// <param name="sessionEvent">The event.</param>
    public void Append(SessionEvent sessionEvent) => this.Append(sessionEvent.Kind.ToString(), JsonSerializer.Serialize(sessionEvent, EventOptions));

    private void Append(string kind, string json)
    {
        lock (this._sync)
        {
            this._stream ??= new FileStream(this._path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
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

            if (this._stream.Length <= bytes.Length)
            {
                this._firstSequenceOfFile = sequence;
            }

            this._sequence = sequence;
            this._lastHash = hash;
            this.RotateIfTooLarge();
        }
    }

    private void RotateIfTooLarge()
    {
        if (this._maximumFileBytes <= 0 || this._stream is not { } stream || stream.Length < this._maximumFileBytes)
        {
            return;
        }

        try
        {
            stream.Dispose();
            this._stream = null;
            var directory = Path.GetDirectoryName(this._path) ?? string.Empty;
            var archive = Path.Combine(directory, Path.GetFileNameWithoutExtension(this._path) + "." + this._firstSequenceOfFile.ToString("D12") + Path.GetExtension(this._path));
            File.Move(this._path, archive);
            this._firstSequenceOfFile = this._sequence + 1;
        }
        catch (IOException)
        {
            // The entry is written; rotating is tried again with the next entry.
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
