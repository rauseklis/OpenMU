namespace MUnique.OpenMU.GameLogic;

/// <summary>
/// An applied effect value formatted for its owner's tooltip.
/// </summary>
public sealed record MagicEffectTooltipLine(string Text, bool IsPenalty = false);
