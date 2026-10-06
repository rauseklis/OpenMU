// <copyright file="AuctionHouseGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.AuctionHouse;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for Auction House packets with the 0xD5 identifier.
/// </summary>
[PlugIn]
[Guid("1B942423-75B8-451A-BFD3-DB66F0A53002")]
internal class AuctionHouseGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key.
    /// </summary>
    internal const byte GroupKey = 0xD5;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuctionHouseGroupHandlerPlugIn"/> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The plugin manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public AuctionHouseGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
