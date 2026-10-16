namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Numeric options from the reference installation's ItemAddOption table.
/// </summary>
public static class PremiumBuffCatalog
{
    private const byte SealFamily = 240;
    private const byte ScrollFamily = 241;
    private const byte ElixirFamily = 242;
    private const int HalfHour = 1800;
    private const int OneHour = 3600;
    private const int TenDays = 864000;

    public static IReadOnlyList<PremiumBuffDefinition> Items { get; } =
    [
        new(13, 43, 40, SealFamily, "Seal of Ascension", HalfHour, (Stats.ExperienceRate, 1.5f, AggregateType.Multiplicate)),
        new(13, 44, 41, SealFamily, "Seal of Wealth", OneHour, (Stats.ExperienceRate, 2f, AggregateType.Multiplicate), (PremiumBuffStats.ItemDropBonus, 0.5f, AggregateType.AddRaw)),
        new(13, 45, 42, SealFamily, "Seal of Sustenance", HalfHour, (PremiumBuffStats.ExperienceBlocked, 1, AggregateType.AddRaw)),
        new(13, 59, 43, SealFamily, "Seal of Mobility", HalfHour, (PremiumBuffStats.WarpCommandAvailable, 1, AggregateType.AddRaw)),
        new(14, 72, 44, ScrollFamily, "Scroll of Quickness", OneHour, (Stats.AttackSpeedAny, 15, AggregateType.AddRaw)),
        new(14, 73, 45, ScrollFamily, "Scroll of Defense", OneHour, (Stats.DefenseFinal, 60, AggregateType.AddFinal)),
        new(14, 74, 46, ScrollFamily, "Scroll of Wrath", HalfHour, (Stats.PhysicalBaseDmg, 30, AggregateType.AddRaw)),
        new(14, 75, 47, ScrollFamily, "Scroll of Wizardry", HalfHour, (Stats.MinimumWizBaseDmg, 30, AggregateType.AddRaw), (Stats.MaximumWizBaseDmg, 30, AggregateType.AddRaw)),
        new(14, 76, 48, ScrollFamily, "Scroll of Health", HalfHour, (Stats.MaximumHealth, 200, AggregateType.AddRaw)),
        new(14, 77, 49, ScrollFamily, "Scroll of Mana", HalfHour, (Stats.MaximumMana, 300, AggregateType.AddRaw)),
        new(14, 78, 50, ElixirFamily, "Elixir of Strength", TenDays, (Stats.TotalStrength, 50, AggregateType.AddRaw)),
        new(14, 79, 51, ElixirFamily, "Elixir of Agility", TenDays, (Stats.TotalAgility, 50, AggregateType.AddRaw)),
        new(14, 80, 52, ElixirFamily, "Elixir of Health", TenDays, (Stats.TotalVitality, 50, AggregateType.AddRaw)),
        new(14, 81, 53, ElixirFamily, "Elixir of Energy", TenDays, (Stats.TotalEnergy, 50, AggregateType.AddRaw)),
        new(14, 82, 54, ElixirFamily, "Elixir of Control", TenDays, (Stats.TotalLeadership, 50, AggregateType.AddRaw)),
        new(13, 62, 87, SealFamily, "Seal of Healing", HalfHour, (Stats.ExperienceRate, 1.5f, AggregateType.Multiplicate), (Stats.HealthRecoveryMultiplier, 0.05f, AggregateType.AddRaw)),
        new(13, 63, 88, SealFamily, "Seal of Divinity", HalfHour, (PremiumBuffStats.ItemDropBonus, 0.5f, AggregateType.AddRaw), (Stats.ManaRecoveryMultiplier, 0.05f, AggregateType.AddRaw)),
        new(14, 97, 89, ScrollFamily, "Scroll of Battle", HalfHour, (Stats.CriticalDamageChance, 0.2f, AggregateType.AddRaw)),
        new(14, 98, 90, ScrollFamily, "Scroll of Strength", HalfHour, (Stats.ExcellentDamageChance, 0.2f, AggregateType.AddRaw)),
        new(13, 81, 99, 243, "Talisman of Guardian", HalfHour, (PremiumBuffStats.DeathProtection, 1, AggregateType.AddRaw)),
        new(13, 82, 100, 244, "Talisman of Item Protection", HalfHour, (PremiumBuffStats.MountDurabilityProtection, 1, AggregateType.AddRaw)),
        new(13, 93, 101, SealFamily, "Master Seal of Ascension", HalfHour, (Stats.MasterExperienceRate, 1.5f, AggregateType.Multiplicate), (PremiumBuffStats.WarpCommandAvailable, 1, AggregateType.AddRaw)),
        new(13, 94, 102, SealFamily, "Master Seal of Wealth", HalfHour, (Stats.MasterExperienceRate, 2f, AggregateType.Multiplicate), (PremiumBuffStats.ItemDropBonus, 0.5f, AggregateType.AddRaw), (PremiumBuffStats.WarpCommandAvailable, 1, AggregateType.AddRaw)),
        new(13, 96, 104, 245, "Seal of Strength", HalfHour, (Stats.AttackRatePvm, 50, AggregateType.AddFinal), (Stats.DefenseRatePvm, 60, AggregateType.AddFinal)),
        new(13, 116, 119, 246, "Seal of Wealth (item drop)", HalfHour, (PremiumBuffStats.ItemDropBonus, 0.5f, AggregateType.AddRaw)),
        new(14, 140, 121, 247, "Scroll of Healing", HalfHour, (Stats.HealthRecoveryMultiplier, 0.03f, AggregateType.AddRaw), (Stats.MaximumHealth, 100, AggregateType.AddRaw)),
    ];

    public static PremiumBuffDefinition? Find(byte group, short number) => Items.FirstOrDefault(item => item.Group == group && item.Number == number);

    public static PremiumBuffDefinition? FindEffect(short effect) => Items.FirstOrDefault(item => item.Effect == effect);

    public static bool ProtectsMount(Item item, IAttributeSystem attributes) => attributes[PremiumBuffStats.MountDurabilityProtection] > 0
        && item.Definition is { Group: 13, Number: 3 or 4 or 37 };
}
