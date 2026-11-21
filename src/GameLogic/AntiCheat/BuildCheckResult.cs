// <copyright file="BuildCheckResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AntiCheat;

/// <summary>
/// The result of comparing a client build identifier with the allowed builds.
/// </summary>
public enum BuildCheckResult
{
    /// <summary>Nothing was compared (off, or no allowed builds are configured).</summary>
    NotChecked,

    /// <summary>The build is in the list of allowed builds.</summary>
    Known,

    /// <summary>The build is not in the list of allowed builds.</summary>
    Unknown,
}
