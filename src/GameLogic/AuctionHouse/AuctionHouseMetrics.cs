// <copyright file="AuctionHouseMetrics.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.AuctionHouse;

using System.Diagnostics.Metrics;
using MUnique.OpenMU.Persistence.AuctionHouse;

/// <summary>
/// The Auction House instruments. Labels are bounded: operation names, outcome names, currency modes, cost classes, invariant
/// names and delivery results. No identifiers are used as labels.
/// </summary>
public static class AuctionHouseMetrics
{
    /// <summary>
    /// The name of the meter which carries every Auction House instrument.
    /// </summary>
    public const string MeterName = "MUnique.OpenMU.AuctionHouse";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> OperationResults = Meter.CreateCounter<long>("auction.operation.results");
    private static readonly Counter<long> FeesRemoved = Meter.CreateCounter<long>("auction.fees.removed");
    private static readonly Counter<long> RateLimited = Meter.CreateCounter<long>("auction.rate_limited");
    private static readonly Counter<long> ExpiriesFinalized = Meter.CreateCounter<long>("auction.expiries.finalized");
    private static readonly Counter<long> Notifications = Meter.CreateCounter<long>("auction.notifications");
    private static readonly Counter<long> ReconciliationFindings = Meter.CreateCounter<long>("auction.reconciliation.findings");

    /// <summary>
    /// Records the outcome of one operation. A replay is recorded as its own outcome, so it is never mistaken for a new event.
    /// </summary>
    /// <param name="operation">The operation: create, bid, buyout, cancel or collect.</param>
    /// <param name="outcome">The outcome name as the repository returned it.</param>
    public static void RecordOperation(string operation, string outcome)
        => OperationResults.Add(1, new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Records the listing fee of a newly created listing, per currency. Fruit fees are recorded per component.
    /// </summary>
    /// <param name="currency">The currency of the listing.</param>
    /// <param name="scalarFee">The fee of a Zen or jewel listing.</param>
    /// <param name="fruitFee">The fee basket of a fruit listing.</param>
    public static void RecordListingFee(AuctionCurrencyMode currency, long scalarFee, FruitBasket? fruitFee)
    {
        if (currency != AuctionCurrencyMode.Fruits)
        {
            FeesRemoved.Add(scalarFee, new KeyValuePair<string, object?>("currency", currency.ToString()));
            return;
        }

        if (fruitFee is not { } fee)
        {
            return;
        }

        foreach (var component in Enum.GetValues<FruitComponent>())
        {
            var amount = fee.Get(component);
            if (amount > 0)
            {
                FeesRemoved.Add(amount, new KeyValuePair<string, object?>("currency", "Fruits"), new KeyValuePair<string, object?>("component", component.ToString()));
            }
        }
    }

    /// <summary>
    /// Records a request refused by the per-account budget.
    /// </summary>
    /// <param name="cost">The cost class of the request.</param>
    public static void RecordRateLimited(AuctionRequestCost cost)
        => RateLimited.Add(1, new KeyValuePair<string, object?>("cost", cost.ToString()));

    /// <summary>
    /// Records the listings finalized by one expiry batch.
    /// </summary>
    /// <param name="finalized">The number of listings finalized.</param>
    public static void RecordExpiries(int finalized)
    {
        if (finalized > 0)
        {
            ExpiriesFinalized.Add(finalized);
        }
    }

    /// <summary>
    /// Records the results of one notification batch.
    /// </summary>
    /// <param name="delivered">Notifications shown to online players.</param>
    /// <param name="notOnline">Notifications whose account was offline.</param>
    /// <param name="failed">Notifications whose delivery threw.</param>
    public static void RecordNotifications(int delivered, int notOnline, int failed)
    {
        Notifications.Add(delivered, new KeyValuePair<string, object?>("result", "delivered"));
        Notifications.Add(notOnline, new KeyValuePair<string, object?>("result", "not_online"));
        Notifications.Add(failed, new KeyValuePair<string, object?>("result", "failed"));
    }

    /// <summary>
    /// Records the broken invariants found by a reconciliation run.
    /// </summary>
    /// <param name="findings">The findings.</param>
    public static void RecordReconciliationFindings(IEnumerable<AuctionReconciliationFinding> findings)
    {
        foreach (var finding in findings)
        {
            ReconciliationFindings.Add(1, new KeyValuePair<string, object?>("invariant", finding.Invariant));
        }
    }
}
