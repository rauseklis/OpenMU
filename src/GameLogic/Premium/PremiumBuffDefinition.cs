namespace MUnique.OpenMU.GameLogic.Premium;

using MUnique.OpenMU.AttributeSystem;

/// <summary>
/// A reference shop item's effect, independent of its purchased duration.
/// </summary>
public sealed record PremiumBuffDefinition(
    byte Group,
    short Number,
    byte Effect,
    byte SubType,
    string Name,
    int DefaultSeconds,
    params (AttributeDefinition Attribute, float Value, AggregateType Aggregation)[] Bonuses);
