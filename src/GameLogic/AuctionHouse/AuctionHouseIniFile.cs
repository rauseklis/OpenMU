// <copyright file="AuctionHouseIniFile.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.IO;

/// <summary>
/// The <c>AuctionHouse.ini</c> on disk. It is created from the current settings when it does not exist yet, and read
/// again whenever its modification time changes, so the owner can edit it while the server runs.
/// </summary>
public sealed class AuctionHouseIniFile
{
    private DateTime _lastWriteUtc = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseIniFile"/> class.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    public AuctionHouseIniFile(string path)
    {
        this.Path = path;
    }

    /// <summary>
    /// Gets the path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Reads the file into the configuration if it is new or has changed since the last read. A missing file is first
    /// created from the configuration's current values.
    /// </summary>
    /// <param name="configuration">The configuration to update.</param>
    /// <returns>The result of the read, or <c>null</c> when the file has not changed.</returns>
    public AuctionHouseIniResult? RefreshIfChanged(AuctionHouseConfiguration configuration)
    {
        try
        {
            if (!File.Exists(this.Path))
            {
                var directory = System.IO.Path.GetDirectoryName(this.Path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(this.Path, AuctionHouseIni.CreateText(configuration));
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(this.Path);
            if (lastWriteUtc == this._lastWriteUtc)
            {
                return null;
            }

            var text = File.ReadAllText(this.Path);
            this._lastWriteUtc = lastWriteUtc;
            return AuctionHouseIni.Apply(configuration, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file is being written or cannot be reached right now; it is tried again on the next refresh.
            return new AuctionHouseIniResult(0, new[] { $"{this.Path} could not be read: {exception.Message}" });
        }
    }
}
