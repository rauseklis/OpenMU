namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.Items;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Adds premium effects to existing Season 6 configurations without reinitializing data.
/// </summary>
[PlugIn]
[Guid("EC5CBF0D-BDBB-4D05-94FC-00666D279DF3")]
public sealed class AddPremiumBuffsUpdatePlugIn : UpdatePlugInBase
{
    public override string Name => "Add reference premium buff effects";
    public override string Description => "Adds missing seals, scrolls, elixirs and protection talismans while preserving configured effects and existing items.";
    public override UpdateVersion Version => UpdateVersion.AddPremiumBuffs;
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;
    public override bool IsMandatory => true;
    public override DateTime CreatedAt => new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        new PremiumBuffs(context, gameConfiguration).Initialize();
        return ValueTask.CompletedTask;
    }
}
