// <copyright file="ResetNpcStatus.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// The answer which the window of the reset NPC shows. The values are the wire values of the ResetNpcStateResponse.
/// </summary>
public enum ResetNpcOutcome : byte
{
    /// <summary>The requirements and the current values, to show the window.</summary>
    Info = 0,

    /// <summary>The reset was done.</summary>
    Done = 1,

    /// <summary>A requirement is not met.</summary>
    RequirementsNotMet = 2,

    /// <summary>The reset limit is reached.</summary>
    LimitReached = 3,

    /// <summary>The request was refused (state, rate limit, the NPC is not open, reset not available).</summary>
    Refused = 4,
}

/// <summary>
/// The requirements of the next reset together with the current values of the player.
/// </summary>
/// <param name="Outcome">What this status tells.</param>
/// <param name="NextResetNumber">The number the next reset has.</param>
/// <param name="MaximumResets">The most resets a character can do (0 = no limit).</param>
/// <param name="RequiredLevel">The level which is required.</param>
/// <param name="CurrentLevel">The current level.</param>
/// <param name="RequiredZen">The Zen which is required.</param>
/// <param name="CurrentZen">The Zen of the player.</param>
/// <param name="RequiredFruits">The fruits which are required (of any type, taken from the Jewel Bank).</param>
/// <param name="BankFruits">The fruits in the Jewel Bank of the player.</param>
/// <param name="WornItems">The number of items the character wears.</param>
/// <param name="GearRuleActive">Whether the reset requires that nothing is worn.</param>
public readonly record struct ResetNpcStatus(
    ResetNpcOutcome Outcome,
    int NextResetNumber,
    int MaximumResets,
    int RequiredLevel,
    int CurrentLevel,
    int RequiredZen,
    int CurrentZen,
    int RequiredFruits,
    long BankFruits,
    int WornItems = 0,
    bool GearRuleActive = false)
{
    /// <summary>The flag of <see cref="Flags"/> which tells that the level is enough.</summary>
    public const byte LevelEnough = 1;

    /// <summary>The flag of <see cref="Flags"/> which tells that the Zen is enough.</summary>
    public const byte ZenEnough = 2;

    /// <summary>The flag of <see cref="Flags"/> which tells that the fruits are enough.</summary>
    public const byte FruitsEnough = 4;

    /// <summary>The flag of <see cref="Flags"/> which tells that the reset limit is reached.</summary>
    public const byte LimitReachedFlag = 8;

    /// <summary>The flag of <see cref="Flags"/> which tells that the equipment requirement is met (or does not apply).</summary>
    public const byte GearOk = 16;

    /// <summary>The flag of <see cref="Flags"/> which tells that the reset requires that nothing is worn.</summary>
    public const byte GearRule = 32;

    /// <summary>
    /// Gets the flags of the wire format: which requirements are met.
    /// </summary>
    public byte Flags
    {
        get
        {
            byte flags = 0;
            if (this.CurrentLevel >= this.RequiredLevel)
            {
                flags |= LevelEnough;
            }

            if (this.CurrentZen >= this.RequiredZen)
            {
                flags |= ZenEnough;
            }

            if (this.BankFruits >= this.RequiredFruits)
            {
                flags |= FruitsEnough;
            }

            if (this.MaximumResets > 0 && this.NextResetNumber > this.MaximumResets)
            {
                flags |= LimitReachedFlag;
            }

            if (!this.GearRuleActive || this.WornItems == 0)
            {
                flags |= GearOk;
            }

            if (this.GearRuleActive)
            {
                flags |= GearRule;
            }

            return flags;
        }
    }

    /// <summary>
    /// Gets a value indicating whether every requirement is met and the limit is not reached.
    /// </summary>
    public bool CanReset => (this.Flags & (LevelEnough | ZenEnough | FruitsEnough | LimitReachedFlag | GearOk)) == (LevelEnough | ZenEnough | FruitsEnough | GearOk);

    /// <summary>
    /// Creates the same status with another outcome.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The status.</returns>
    public ResetNpcStatus WithOutcome(ResetNpcOutcome outcome) => this with { Outcome = outcome };
}
