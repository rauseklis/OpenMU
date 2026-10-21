// <copyright file="IUpdateVaultNumberPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Views.Vault;

/// <summary>
/// Interface of a view whose implementation tells the client which vault is currently active.
/// </summary>
public interface IUpdateVaultNumberPlugIn : IViewPlugIn
{
    /// <summary>
    /// Tells the client which vault is currently active and how many vaults the account has.
    /// </summary>
    /// <param name="success">If set to <c>true</c>, the vault was switched; Otherwise, the request was refused.</param>
    ValueTask UpdateVaultNumberAsync(bool success);
}
