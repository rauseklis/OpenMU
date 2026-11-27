// <copyright file="DarkInvasionBothServersUpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.PlugIns.InvasionEvents;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Lets the Dark Skeleton invasion run on game server 1 (Ember 5x) too, where its monsters have half of their strength.
/// The other settings of the invasion are kept.
/// </summary>
[PlugIn]
[Guid("C3F1A6D2-7B4E-4E19-9D85-2A6F0B3C8E57")]
public sealed class DarkInvasionBothServersUpdatePlugIn : UpdatePlugInBase
{
    /// <summary>The game server which gets the invasion with weaker monsters.</summary>
    internal const byte WeakerServerId = 1;

    /// <summary>The strength of the monsters on that server, in percent.</summary>
    internal const float WeakerStrengthPercentage = 50f;

    /// <inheritdoc />
    public override string Name => "Dark Skeleton invasion on both servers";

    /// <inheritdoc />
    public override string Description => "The Dark Skeleton invasion also runs on game server 1 (internal id 1), with the monsters at 50% strength there.";

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.DarkInvasionBothServers;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var typeId = typeof(DarkSkeletonInvasionPlugIn).GUID;
        var plugInConfiguration = gameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration is null)
        {
            // Not created yet: the server creates it at start with the default configuration, which already includes both servers.
            return ValueTask.CompletedTask;
        }

        // The server writes plug-in configurations with empty reference metadata ("$id": "", "$values").
        var configuration = plugInConfiguration.GetConfiguration<DarkSkeletonInvasionConfiguration>(ServerLikeReferenceHandler.Instance)
            ?? DarkSkeletonInvasionConfiguration.Default;

        if (configuration.GameServerIds.Count > 0 && !configuration.GameServerIds.Contains(WeakerServerId))
        {
            configuration.GameServerIds.Add(WeakerServerId);
        }

        configuration.WeakerGameServerIds = [WeakerServerId];
        configuration.WeakerStrengthPercentage = WeakerStrengthPercentage;
        plugInConfiguration.SetConfiguration(configuration, ServerLikeReferenceHandler.Instance);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Reads and writes reference metadata like the server's data source reference handler does for a configuration
    /// without references to other configuration objects: every "$id" is empty and is not remembered.
    /// </summary>
    public sealed class ServerLikeReferenceHandler : ReferenceHandler
    {
        /// <summary>Gets the instance.</summary>
        public static ServerLikeReferenceHandler Instance { get; } = new();

        /// <inheritdoc />
        public override ReferenceResolver CreateResolver() => new Resolver();

        private sealed class Resolver : ReferenceResolver
        {
            public override void AddReference(string referenceId, object value)
            {
                // Nothing to remember: the configuration has no references.
            }

            public override string GetReference(object value, out bool alreadyExists)
            {
                alreadyExists = false;
                return string.Empty;
            }

            public override object ResolveReference(string referenceId)
                => throw new KeyNotFoundException($"The configuration has no reference '{referenceId}'.");
        }
    }
}
