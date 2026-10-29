// <copyright file="DurabilityProtection.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Pet;

/// <summary>Evaluates only durability master skills, using the same percentage as the client display.</summary>
public static class DurabilityProtection
{
    /// <summary>Whether a master definition is handled here instead of as a raw duration multiplier.</summary>
    public static bool Handles(Skill skill) => skill.Number is 300 or 305 or 312 or 578 or 583 or 590 or 535;

    /// <summary>Gets the active displayed protection percentage, without replaced-tier stacking.</summary>
    public static double Percent(Player player, Item item)
    {
        var trainable = item.IsTrainablePet();
        var category = trainable ? 0 : PetDurabilityPolicy.IsPet(item) ? 3 : item.ItemSlot >= InventoryConstants.PendantSlot ? 2 : 1;
        var entries = player.SelectedCharacter?.LearnedSkills
            .Where(e => e.Level > 0 && e.Skill is { MasterDefinition: not null } s && Handles(s))
            .ToList();
        if (entries is null || entries.Count == 0)
        {
            return 0;
        }

        // Prerequisite tiers are separate categories. Actual ReplacedSkill chains exclude predecessors.
        var active = entries.Where(e => category == (e.Skill!.Number switch { 535 => 0, 300 or 578 => 1, 305 or 583 => 2, _ => 3 })
                && !entries.Any(other => !ReferenceEquals(other, e) && other.Skill!.GetBaseSkills().Contains(e.Skill!)))
            .OrderByDescending(e => e.Skill!.MasterDefinition!.Rank).ThenByDescending(e => e.Level).FirstOrDefault();
        return active is null ? 0 : Math.Clamp(active.CalculateDisplayValue(), 0, 100);
    }
}
