// <copyright file="AuctionHouseRepository.Bank.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AuctionHouse;

using System.Threading;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Persistence.EntityFramework.JewelBank;
using MUnique.OpenMU.Persistence.EntityFramework.Model;
using MUnique.OpenMU.Persistence.JewelBank;

/// <summary>
/// The Jewel Bank part of the repository. Jewels and stat fruits are paid out of the account's Jewel Bank and claimed into it, inside the
/// same transaction as the auction change, so a failure rolls back both. Zen and items never use the bank.
/// </summary>
public sealed partial class AuctionHouseRepository
{
    private static AuctionClaimOutcome ClaimOutcomeOf(JewelMaterialization result) => result switch
    {
        JewelMaterialization.InventoryFull => AuctionClaimOutcome.InventoryFull,
        JewelMaterialization.BankLimit => AuctionClaimOutcome.BankLimit,
        _ => AuctionClaimOutcome.UnsupportedCurrency,
    };

    private static async Task<JewelTender> BankPayJewelsAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, Guid listingId, byte currency, long requiredUnits, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (AuctionBankWallet.JewelCode(currency) is not { } code)
        {
            return JewelTender.NotConfigured;
        }

        var state = await JewelBankRepository.ReadStateAsync(context, accountId, cancellationToken);
        var packCodes = new[] { AuctionBankWallet.PackCode(code, 0), AuctionBankWallet.PackCode(code, 1), AuctionBankWallet.PackCode(code, 2) };
        var plan = BankJewelPaymentPlanner.Plan(requiredUnits, state.CountOf(code), packCodes.Select(state.CountOf).ToArray());
        if (plan is null)
        {
            return JewelTender.Insufficient;
        }

        if (plan.LooseUsed > 0 && !await JewelBankRepository.DebitAsync(context, accountId, characterId, operationId, code, plan.LooseUsed, now, cancellationToken, JewelBankRepository.AuctionPayReason))
        {
            return JewelTender.Insufficient;
        }

        var consumedUnits = plan.LooseUsed;
        var sizes = new[] { 10, 20, 30 };
        for (var index = 0; index < packCodes.Length; index++)
        {
            if (plan.PacksUsed[index] <= 0)
            {
                continue;
            }

            if (!await JewelBankRepository.DebitAsync(context, accountId, characterId, operationId, packCodes[index], plan.PacksUsed[index], now, cancellationToken, JewelBankRepository.AuctionPayReason))
            {
                return JewelTender.Insufficient;
            }

            consumedUnits += plan.PacksUsed[index] * sizes[index];
        }

        if (plan.ChangeUnits > 0
            && await JewelBankRepository.CreditAsync(context, accountId, characterId, operationId, code, plan.ChangeUnits, JewelBankLimits.MaximumPerKind, JewelBankRepository.AuctionClaimReason, now, cancellationToken) is null)
        {
            return JewelTender.Insufficient;
        }

        var tenderIn = Movement(accountId, operationId, listingId, null, consumedUnits, TenderInKind, now);
        tenderIn.Currency = currency;
        context.Set<AuctionCurrencyMovement>().Add(tenderIn);
        if (plan.ChangeUnits > 0)
        {
            var changeOut = Movement(accountId, operationId, listingId, null, plan.ChangeUnits, ChangeOutKind, now);
            changeOut.Currency = currency;
            context.Set<AuctionCurrencyMovement>().Add(changeOut);
        }

        return JewelTender.Paid;
    }

    private static async Task<bool> BankPayFruitsAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, Guid listingId, FruitBasket amount, byte kind, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var component in Enum.GetValues<FruitComponent>())
        {
            var count = amount.Get(component);
            if (count <= 0)
            {
                continue;
            }

            if (!await JewelBankRepository.DebitAsync(context, accountId, characterId, operationId, AuctionBankWallet.FruitCode(component), count, now, cancellationToken, JewelBankRepository.AuctionPayReason))
            {
                return false;
            }

            var movement = Movement(accountId, operationId, listingId, null, count, kind, now);
            movement.Currency = FruitsCurrency;
            movement.FruitComponent = (byte)component;
            context.Set<AuctionCurrencyMovement>().Add(movement);
        }

        return true;
    }

    /// <summary>
    /// Credits jewels and fruits which are claimed from the Mailbox to the account's Jewel Bank, as loose units. Either every unit is
    /// credited or the caller rolls back; a kind which would exceed the bank's per-kind limit refuses the whole claim.
    /// </summary>
    private static async Task<JewelMaterialization> BankCreditAsync(EntityDataContext context, Guid accountId, Guid characterId, Guid operationId, IEnumerable<(byte Code, long Units)> units, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var (code, amount) in units)
        {
            if (amount <= 0)
            {
                continue;
            }

            if (await JewelBankRepository.CreditAsync(context, accountId, characterId, operationId, code, amount, JewelBankLimits.MaximumPerKind, JewelBankRepository.AuctionClaimReason, now, cancellationToken) is null)
            {
                return JewelMaterialization.BankLimit;
            }
        }

        return JewelMaterialization.Materialized;
    }
}
