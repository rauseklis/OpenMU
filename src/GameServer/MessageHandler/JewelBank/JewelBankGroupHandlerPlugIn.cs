// <copyright file="JewelBankGroupHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.JewelBank;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Packet handler for Jewel Bank packets with the 0xD6 identifier.
/// </summary>
[PlugIn]
[Guid("ACFD3350-EFC3-4A6A-B355-D1E012E1BBCF")]
internal class JewelBankGroupHandlerPlugIn : GroupPacketHandlerPlugIn
{
    /// <summary>
    /// The group key.
    /// </summary>
    internal const byte GroupKey = 0xD6;

    /// <summary>
    /// Initializes a new instance of the <see cref="JewelBankGroupHandlerPlugIn"/> class.
    /// </summary>
    /// <param name="clientVersionProvider">The client version provider.</param>
    /// <param name="manager">The plugin manager.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public JewelBankGroupHandlerPlugIn(IClientVersionProvider clientVersionProvider, PlugInManager manager, ILoggerFactory loggerFactory)
        : base(clientVersionProvider, manager, loggerFactory)
    {
    }

    /// <inheritdoc/>
    public override bool IsEncryptionExpected => true;

    /// <inheritdoc/>
    public override byte Key => GroupKey;
}
