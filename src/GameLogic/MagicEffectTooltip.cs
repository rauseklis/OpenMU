namespace MUnique.OpenMU.GameLogic;

using System.Globalization;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Premium;

/// <summary>
/// Describes applied power-ups without recalculating the caster's skill formulas.
/// </summary>
public static class MagicEffectTooltip
{
    private static readonly IReadOnlyDictionary<AttributeDefinition, string> Labels = new Dictionary<AttributeDefinition, string>
    {
        [Stats.GreaterDamageBonus] = "Attack damage",
        [Stats.GreaterDefenseBonus] = "Defense",
        [Stats.CriticalDamageBonus] = "Critical damage",
        [Stats.CriticalDamageChance] = "Critical hit chance",
        [Stats.DefenseIgnoreChance] = "Ignore defense chance",
        [Stats.SwellLifeHealthIncrease] = "Maximum HP",
        [Stats.SwellLifeManaIncrease] = "Maximum mana",
        [Stats.SoulBarrierReceiveDecrement] = "Damage reduction",
        [Stats.SoulBarrierManaTollPerHit] = "Mana consumed per hit",
        [Stats.TotalVitality] = "Vitality",
        [Stats.IncreaseBlockBonus] = "Defense success rate (PvM)",
        [Stats.DefenseFinal] = "Defense",
        [Stats.DefenseRatePvm] = "Defense success rate (PvM)",
        [Stats.DefenseRatePvp] = "Defense success rate (PvP)",
        [Stats.MinimumWizBaseDmg] = "Minimum wizardry damage",
        [Stats.MaximumWizBaseDmg] = "Maximum wizardry damage",
        [Stats.AttackDamageIncrease] = "Damage",
        [Stats.DamageReflection] = "Damage reflection",
        [Stats.DefenseDecrement] = "Defense",
        [Stats.InnovationDefDecrement] = "Defense",
        [Stats.WeaknessPhysDmgDecrement] = "Physical damage",
        [Stats.AmmunitionConsumptionRate] = "Ammunition consumption",
        [Stats.MaximumHealth] = "Maximum HP",
        [Stats.MaximumMana] = "Maximum mana",
        [Stats.AttackSpeedAny] = "Attack and magic speed",
        [Stats.BaseDamageBonus] = "Base damage",
        [Stats.DefenseBase] = "Base defense",
        [Stats.DamageReceiveDecrement] = "Incoming damage",
        [Stats.AbilityRecoveryAbsolute] = "AG recovery",
        [Stats.IsInvisible] = "Invisibility",
        [Stats.IsAsleep] = "Sleep",
        [Stats.IsStunned] = "Stun",
        [Stats.IsBleeding] = "Bleeding",
        [Stats.ExperienceRate] = "Normal EXP",
        [Stats.MasterExperienceRate] = "Master EXP",
        [Stats.BonusExperienceRate] = "Normal / master EXP bonus",
        [Stats.TotalStrength] = "Strength",
        [Stats.TotalAgility] = "Agility",
        [Stats.TotalEnergy] = "Energy",
        [Stats.TotalLeadership] = "Command",
        [Stats.PhysicalBaseDmg] = "Physical attack",
        [Stats.ExcellentDamageChance] = "Excellent hit chance",
        [Stats.AttackRatePvm] = "Attack success rate (PvM)",
        [Stats.AttackRatePvp] = "Attack success rate (PvP)",
        [Stats.HealthRecoveryMultiplier] = "Max HP recovered per tick",
        [Stats.ManaRecoveryMultiplier] = "Max mana recovered per tick",
        [Stats.AbilityRecoveryMultiplier] = "Max AG recovered per tick",
        [Stats.ShieldRecoveryMultiplier] = "Max SD recovered per tick",
        [Stats.HealthRecoveryAbsolute] = "HP recovered per tick",
        [Stats.ManaRecoveryAbsolute] = "Mana recovered per tick",
        [Stats.ShieldRecoveryAbsolute] = "SD recovered per tick",
        [PremiumBuffStats.ItemDropBonus] = "Item drop bonus",
        [PremiumBuffStats.ExperienceBlocked] = "EXP gain disabled",
        [PremiumBuffStats.DeathProtection] = "Death penalty protection",
        [PremiumBuffStats.MountDurabilityProtection] = "Mount durability protected (pets excluded)",
        [PremiumBuffStats.WarpCommandAvailable] = "Warp command available",
    };

    private static readonly HashSet<AttributeDefinition> PercentBonuses =
    [
        Stats.CriticalDamageChance, Stats.DefenseIgnoreChance, Stats.SwellLifeHealthIncrease,
        Stats.SwellLifeManaIncrease, Stats.SoulBarrierReceiveDecrement, Stats.DamageReflection,
        Stats.BonusExperienceRate, Stats.ExcellentDamageChance, PremiumBuffStats.ItemDropBonus,
        Stats.HealthRecoveryMultiplier, Stats.ManaRecoveryMultiplier, Stats.AbilityRecoveryMultiplier, Stats.ShieldRecoveryMultiplier,
    ];

    /// <summary>
    /// Gets the numeric bonuses and penalties of the actual effect on its recipient.
    /// </summary>
    public static IReadOnlyList<MagicEffectTooltipLine> Build(MagicEffect effect, IAttributeSystem recipient)
    {
        var powerUps = effect.PowerUpElements.ToArray();
        if (powerUps.Any(power => power.Target == Stats.BerserkerManaMultiplier))
        {
            return BuildBerserker(recipient);
        }

        var lines = new List<MagicEffectTooltipLine>();
        foreach (var group in powerUps.GroupBy(power => power.Target))
        {
            var target = group.Key;
            var value = Aggregate(group.Select(power => power.Element).ToArray());
            if (target.MaximumValue is { } maximum)
            {
                value = Math.Min(value, maximum);
            }

            var label = Labels.GetValueOrDefault(target) ?? target.Designation ?? "Effect";
            var percent = PercentBonuses.Contains(target);
            if (target == Stats.IncreaseBlockBonus || target == Stats.SoulBarrierManaTollPerHit)
            {
                value = recipient[target];
            }

            if (target == Stats.SoulBarrierReceiveDecrement)
            {
                value = Math.Clamp(recipient[target], 0, 0.9f);
            }

            if (target == Stats.WeaknessPhysDmgDecrement || target == Stats.InnovationDefDecrement)
            {
                value = -value;
                percent = true;
            }
            else if (group.All(power => power.Element.AggregateType == AggregateType.Multiplicate))
            {
                value -= 1;
                percent = true;
            }

            if (IsStatus(target))
            {
                if (value != 0)
                {
                    lines.Add(new(label));
                }

                continue;
            }

            if (Math.Abs(value) > 0.00001f)
            {
                var penalty = value < 0;
                if (target == Stats.AmmunitionConsumptionRate || target == Stats.DamageReceiveDecrement)
                {
                    penalty = value > 0;
                }

                lines.Add(Format(label, value, percent, penalty || target == Stats.SoulBarrierManaTollPerHit));
            }
        }

        return lines;
    }

    private static float Aggregate(IElement[] elements)
    {
        var raw = elements.Where(element => element.AggregateType == AggregateType.AddRaw).Sum(element => element.Value);
        raw += elements.Where(element => element.AggregateType == AggregateType.Maximum).Select(element => element.Value).DefaultIfEmpty().Max();
        if (elements.All(element => element.AggregateType == AggregateType.Multiplicate))
        {
            raw = 1;
        }

        var multiplier = elements.Where(element => element.AggregateType == AggregateType.Multiplicate).Aggregate(1f, (product, element) => product * element.Value);
        return (raw * multiplier) + elements.Where(element => element.AggregateType == AggregateType.AddFinal).Sum(element => element.Value);
    }

    private static IReadOnlyList<MagicEffectTooltipLine> BuildBerserker(IAttributeSystem recipient)
    {
        var healthPenalty = Math.Min(-0.1f, recipient[Stats.BerserkerHealthDecrement]) * Math.Min(1, recipient[Stats.BerserkerMinPhysDmgBonus]);
        var lines = new List<MagicEffectTooltipLine>
        {
            Format("Maximum mana", recipient[Stats.BerserkerManaMultiplier], true),
            Format("Base wizardry damage", recipient[Stats.BerserkerWizardryMultiplier], true),
            Format("Base curse damage", recipient[Stats.BerserkerCurseMultiplier], true),
            Format("Minimum physical damage", recipient[Stats.BerserkerMinPhysDmgBonus]),
            Format("Maximum physical damage", recipient[Stats.BerserkerMaxPhysDmgBonus]),
            Format("Minimum wizardry damage", recipient[Stats.BerserkerMinWizDmgBonus]),
            Format("Maximum wizardry damage", recipient[Stats.BerserkerMaxWizDmgBonus]),
            Format("Minimum curse damage", recipient[Stats.BerserkerMinCurseDmgBonus]),
            Format("Maximum curse damage", recipient[Stats.BerserkerMaxCurseDmgBonus]),
            Format("Maximum HP", healthPenalty, true, true),
            Format("Stat-based defense", healthPenalty, true, true),
        };
        if (recipient[Stats.BerserkerProficiencyMultiplier] != 0)
        {
            lines.Add(Format("Final wizardry / curse damage", recipient[Stats.BerserkerProficiencyMultiplier], true));
        }

        return lines;
    }

    private static MagicEffectTooltipLine Format(string label, float value, bool percent = false, bool penalty = false)
    {
        var display = percent ? value * 100 : value;
        var number = display.ToString("+0.##;-0.##;0", CultureInfo.InvariantCulture);
        return new($"{label}: {number}{(percent ? "%" : string.Empty)}", penalty);
    }

    private static bool IsStatus(AttributeDefinition target) => target == Stats.IsInvisible
        || target == Stats.IsAsleep || target == Stats.IsStunned || target == Stats.IsBleeding
        || target == PremiumBuffStats.ExperienceBlocked || target == PremiumBuffStats.DeathProtection
        || target == PremiumBuffStats.MountDurabilityProtection || target == PremiumBuffStats.WarpCommandAvailable;
}
