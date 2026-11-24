// <copyright file="MonsterBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The monster balance: the x5 server gets its own monster scaling, ordinary monsters drop
/// items and zen half as often, and excellent rings and pendants are never dropped.
/// </summary>
/// <remarks>
/// The same rules are used for new databases (at the end of the initialization) and for the existing database
/// (<see cref="Updates.MonsterBalanceUpdatePlugIn"/>, installed once). Both game servers (x5 and x500) share this
/// configuration; only the experience rate and the small bonus drops are per server and are not touched here.
/// </remarks>
public sealed class MonsterBalance(IContext context, GameConfiguration configuration) : InitializerBase(context, configuration)
{
    /// <summary>The internal id of the x500 server (Inferno), which keeps the scaling it already has.</summary>
    public const byte MainServerId = 0;

    /// <summary>The attack rate increase in percent on the x500 server, used only when it was not configured yet.</summary>
    public const float MainServerAttackRatePercent = 150f;

    /// <summary>The health increase in percent on the x500 server, used only when it was not configured yet.</summary>
    public const float MainServerHealthPercent = 20f;

    /// <summary>The internal id of the x5 server (Ember), which gets its own scaling.</summary>
    public const byte OtherServerId = 1;

    /// <summary>The attack rate increase in percent on the x5 server.</summary>
    public const float OtherServerAttackRatePercent = 50f;

    /// <summary>The health increase in percent on the x5 server.</summary>
    public const float OtherServerHealthPercent = 20f;

    /// <summary>The factor of the chance of the common loot groups.</summary>
    public const double DropChanceFactor = 0.5;

    /// <summary>
    /// Gets a value indicating whether the group is one of the common loot groups which every monster on a map can
    /// drop from: zen, random items, the random excellent items and the jewels. Boss, event, quest, box and bonus
    /// groups, and groups with a guaranteed drop, are not.
    /// </summary>
    /// <param name="configuration">The game configuration.</param>
    /// <param name="group">The drop group.</param>
    /// <returns><c>true</c>, if it's a common loot group.</returns>
    public static bool IsCommonLootGroup(GameConfiguration configuration, DropItemGroup group)
    {
        return group is not ItemDropItemGroup
            && group.Monster is null
            && group.Chance is > 0 and < 1
            && group.ItemType is SpecialItemType.Money or SpecialItemType.RandomItem or SpecialItemType.Excellent or SpecialItemType.Jewel
            && configuration.Maps.Any(map => map.DropItemGroups.Contains(group));
    }

    /// <inheritdoc />
    public override void Initialize()
    {
        this.ApplyScalingToEachServer();
        this.HalveCommonLoot();
        this.RemoveExcellentJewelleryFromDrops();
    }

    /// <summary>
    /// Creates the configuration of the monster scaling plug-in, if the database doesn't have it yet.
    /// New databases get it with the other plug-in configurations later, so this is only needed for the update.
    /// </summary>
    public void EnsureScalerConfiguration()
    {
        var typeId = typeof(MonsterAttributeScaler).GUID;
        if (this.GameConfiguration.PlugInConfigurations.Any(c => c.TypeId == typeId))
        {
            return;
        }

        var plugInConfiguration = this.Context.CreateNew<PlugInConfiguration>();
        plugInConfiguration.SetGuid(typeId);
        plugInConfiguration.TypeId = typeId;
        this.GameConfiguration.PlugInConfigurations.Add(plugInConfiguration);
    }

    /// <summary>
    /// Gives the x5 server its own monster scaling (+50 % attack rate, +20 % health). The x500 server keeps the values
    /// it has (+150 % attack rate, +20 % health). The stored monster values are not changed, so nothing is scaled
    /// twice, and no other server is scaled.
    /// </summary>
    private void ApplyScalingToEachServer()
    {
        var typeId = typeof(MonsterAttributeScaler).GUID;
        var plugInConfiguration = this.GameConfiguration.PlugInConfigurations.FirstOrDefault(c => c.TypeId == typeId);
        if (plugInConfiguration is null)
        {
            return;
        }

        var scaler = plugInConfiguration.GetConfiguration<MonsterAttributeScalerConfiguration>(null)
            ?? new MonsterAttributeScalerConfiguration
            {
                DamagePercentage = 0f,
                DefenseRatePercentage = 0f,
                DefensePercentage = 0f,
                AttackRatePercentage = MainServerAttackRatePercent,
                HealthPercentage = MainServerHealthPercent,
                OnlyServerId = MainServerId,
            };
        scaler.OtherServerId = OtherServerId;
        scaler.OtherServerAttackRatePercentage = OtherServerAttackRatePercent;
        scaler.OtherServerHealthPercentage = OtherServerHealthPercent;
        plugInConfiguration.SetConfiguration(scaler, null);
        plugInConfiguration.IsActive = true;
    }

    private void HalveCommonLoot()
    {
        foreach (var group in this.GameConfiguration.DropItemGroups.Where(g => IsCommonLootGroup(this.GameConfiguration, g)))
        {
            group.Chance *= DropChanceFactor;
        }
    }

    private void RemoveExcellentJewelleryFromDrops()
    {
        // Only the loot groups of monsters and maps. The hand-picked contents of boxes (e.g. the Blue Ribbon Box) are not changed here.
        var groups = this.GameConfiguration.DropItemGroups
            .Where(group => group is not ItemDropItemGroup && group.ItemType == SpecialItemType.Excellent);
        foreach (var group in groups)
        {
            foreach (var item in group.PossibleItems.Where(ExcellentJewellery.IsExcellentRingOrPendant).ToList())
            {
                group.PossibleItems.Remove(item);
            }
        }
    }
}
