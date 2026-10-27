// <copyright file="CashChatCommandPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands.Arguments;
using MUnique.OpenMU.Persistence.CashShop;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// A chat command which lets a game master give an online player Cash Shop currency: <c>/cash char wcc wcp gp</c>.
/// </summary>
[PlugIn]
[Display(Name = "Cash Shop Chat Command", Description = "Handles the GM command /cash, which adds WCoinC, WCoinP and Goblin Points to the account of an online character.")]
[Guid("8B2D5E31-4C7A-4E19-A6D0-3F95B7C1E264")]
[ChatCommandHelp(Command, typeof(CashChatCommandArgs), CharacterStatus.GameMaster)]
public class CashChatCommandPlugIn : ChatCommandPlugInBase<CashChatCommandArgs>
{
    private const string Command = "/cash";

    /// <inheritdoc />
    public override string Key => Command;

    /// <inheritdoc/>
    public override CharacterStatus MinCharacterStatusRequirement => CharacterStatus.GameMaster;

    /// <inheritdoc />
    protected override async ValueTask DoHandleCommandAsync(Player gameMaster, CashChatCommandArgs arguments)
    {
        var credit = new CashShopBalance(arguments.WCoinC, arguments.WCoinP, arguments.GoblinPoints);
        if (!credit.IsValid() || credit == new CashShopBalance())
        {
            await gameMaster.ShowBlueMessageAsync($"Give at least one amount between 1 and {CashShopBalance.Maximum}: /cash <char> <wcc> <wcp> <gp>").ConfigureAwait(false);
            return;
        }

        var target = await this.GetPlayerByCharacterNameAsync(gameMaster, arguments.CharacterName ?? string.Empty).ConfigureAwait(false);
        if (target is null)
        {
            return;
        }

        var repository = gameMaster.GameContext.PersistenceContextProvider.CreateCashShopRepository();
        if (repository is null || target.Account is not Persistence.IIdentifiable account)
        {
            await gameMaster.ShowBlueMessageAsync("The Cash Shop is not available.").ConfigureAwait(false);
            return;
        }

        try
        {
            var reason = $"GM {gameMaster.Name} /cash {target.Name}";
            var balance = await repository.CreditAsync(account.Id, target.GetGameServerId(), Guid.NewGuid(), credit, reason).ConfigureAwait(false);
            await gameMaster.ShowBlueMessageAsync($"{target.Name} now has {balance.WCoinC} WCoinC, {balance.WCoinP} WCoinP, {balance.GoblinPoints} GP.").ConfigureAwait(false);
            await target.ShowBlueMessageAsync("Your Cash Shop balance was increased. Reopen the shop to see it.").ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await gameMaster.ShowBlueMessageAsync(ex.Message).ConfigureAwait(false);
        }
    }
}
