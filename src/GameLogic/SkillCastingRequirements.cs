// <copyright file="SkillCastingRequirements.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>Checks casting stats independently of scroll learning and equipment requirements.</summary>
internal static class SkillCastingRequirements
{
    /// <summary>Checks every configured casting requirement except character level.</summary>
    /// <param name="skill">The skill being cast, including Master variants.</param>
    /// <param name="getAttributeValue">Resolves the character's current stat value.</param>
    /// <returns>Whether every non-level casting requirement is met.</returns>
    public static bool AreMet(Skill skill, Func<AttributeDefinition, float?> getAttributeValue)
    {
        foreach (var requirement in skill.Requirements)
        {
            if (requirement.Attribute is not { } attribute || attribute == Stats.Level)
            {
                continue;
            }

            if (getAttributeValue(attribute) is not { } value || value < requirement.MinimumValue)
            {
                return false;
            }
        }

        return true;
    }
}
