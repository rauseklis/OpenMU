// <copyright file="IShowResetNpcPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Reset;

using MUnique.OpenMU.GameLogic.Resets;

/// <summary>
/// Interface of a view whose implementation shows or updates the window of the reset NPC (Raymond the Reset Master).
/// </summary>
public interface IShowResetNpcPlugIn : IViewPlugIn
{
    /// <summary>
    /// Shows the requirements of the next reset together with the current values of the player, or the result of a reset request.
    /// </summary>
    /// <param name="status">The status to show.</param>
    /// <returns>A task which completes when the client is informed.</returns>
    ValueTask ShowResetNpcAsync(ResetNpcStatus status);
}
