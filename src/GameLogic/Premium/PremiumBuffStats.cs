namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.AttributeSystem;

/// <summary>
/// Gameplay switches and persistent expiry keys for premium effects.
/// </summary>
public static class PremiumBuffStats
{
    public static AttributeDefinition ItemDropBonus { get; } = new(new Guid("C67244D1-BA20-4B7B-8617-80C9C2A30001"), "Premium item drop bonus", string.Empty);
    public static AttributeDefinition ExperienceBlocked { get; } = new(new Guid("C67244D1-BA20-4B7B-8617-80C9C2A30002"), "Experience gain disabled", string.Empty);
    public static AttributeDefinition DeathProtection { get; } = new(new Guid("C67244D1-BA20-4B7B-8617-80C9C2A30003"), "Death penalty protection", string.Empty);
    public static AttributeDefinition MountDurabilityProtection { get; } = new(new Guid("C67244D1-BA20-4B7B-8617-80C9C2A30004"), "Mount durability protection", string.Empty);
    public static AttributeDefinition WarpCommandAvailable { get; } = new(new Guid("C67244D1-BA20-4B7B-8617-80C9C2A30005"), "Warp command available", string.Empty);

    public static AttributeDefinition ExpiryDay(byte effect) => new(
        new Guid($"C67244D1-BA20-4B7B-8617-80C9C2B1{effect:X4}"), $"Premium effect {effect} expiry day", string.Empty);

    public static AttributeDefinition ExpirySecond(byte effect) => new(
        new Guid($"C67244D1-BA20-4B7B-8617-80C9C2B2{effect:X4}"), $"Premium effect {effect} expiry second", string.Empty);
}
