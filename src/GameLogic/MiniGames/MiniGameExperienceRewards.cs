// <copyright file="MiniGameExperienceRewards.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MiniGames;

/// <summary>
/// Routes only eligible DS seven completion rewards to Master EXP.
/// </summary>
internal static class MiniGameExperienceRewards
{
    private const byte MasterRewardDevilSquareLevel = 7;

    /// <summary>
    /// Grants a fixed completion reward without monster experience multipliers.
    /// </summary>
    /// <param name="definition">The completed event.</param>
    /// <param name="player">The rewarded player.</param>
    /// <param name="amount">The configured reward.</param>
    /// <returns>The asynchronous operation.</returns>
    internal static ValueTask GiveAsync(MiniGameDefinition definition, Player player, int amount)
    {
        var isMasterReward = definition.Type == MiniGameType.DevilSquare
            && definition.GameLevel == MasterRewardDevilSquareLevel
            && player.SelectedCharacter?.CharacterClass?.IsMasterClass == true
            && player.Level >= player.GameContext.Configuration.MaximumLevel;
        return isMasterReward ? player.AddMasterExperienceRewardAsync(amount) : player.AddExperienceAsync(amount, null);
    }
}
