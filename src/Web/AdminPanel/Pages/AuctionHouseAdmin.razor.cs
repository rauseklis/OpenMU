// <copyright file="AuctionHouseAdmin.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MUnique.OpenMU.GameLogic.AuctionHouse;
using MUnique.OpenMU.Persistence.AuctionHouse;
using MUnique.OpenMU.Web.AdminPanel.Properties;
using MUnique.OpenMU.Web.AdminPanel.Services;

/// <summary>
/// Operator page for the Auction House: invariant checks, repairs, browsing, and administrative removal.
/// </summary>
public partial class AuctionHouseAdmin : ComponentBase
{
    private bool _available = true;
    private bool _busy;
    private bool _findingsChecked;
    private string? _message;
    private IReadOnlyList<AuctionReconciliationFinding> _findings = [];
    private IReadOnlyList<AuctionListingRow> _rows = [];
    private byte _currency;
    private string _listingId = string.Empty;
    private string _reason = string.Empty;

    /// <summary>
    /// Gets or sets the Auction House administration service.
    /// </summary>
    [Inject]
    public AuctionAdministrationService Administration { get; set; } = null!;

    /// <summary>
    /// Gets or sets the authentication state, used to record the administrator in the audit row.
    /// </summary>
    [Inject]
    public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    /// <summary>
    /// Gets the currency options of the browse list.
    /// </summary>
    public IReadOnlyList<(byte Value, string Text)> CurrencyOptions { get; } =
    [
        (0, "Zen"), (1, "Jewel of Chaos"), (2, "Jewel of Bless"), (3, "Jewel of Soul"), (4, "Jewel of Life"),
        (5, "Jewel of Creation"), (6, "Jewel of Guardian"), (7, "Jewel of Harmony"), (8, "Fruit Basket"),
    ];

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this._available = this.Administration.IsAvailable;
    }

    private void OnCurrencyChanged(ChangeEventArgs args)
    {
        this._currency = byte.TryParse(args.Value?.ToString(), out var value) ? value : (byte)0;
    }

    private async Task ReconcileAsync()
    {
        await this.RunAsync(async () =>
        {
            this._findings = await this.Administration.ReconcileAsync() ?? [];
            this._findingsChecked = true;
        });
    }

    private async Task RepairAsync()
    {
        await this.RunAsync(async () =>
        {
            var result = await this.Administration.RepairAsync();
            this._message = result is { } repaired ? string.Format(Resources.AuctionHouseRepairResult, repaired.Repaired, repaired.Skipped) : null;
        });
    }

    private async Task BrowseAsync()
    {
        await this.RunAsync(async () =>
        {
            this._rows = (await this.Administration.BrowseAsync(this._currency))?.Rows ?? [];
        });
    }

    private async Task RemoveAsync()
    {
        if (!Guid.TryParse(this._listingId, out var listingId) || string.IsNullOrWhiteSpace(this._reason))
        {
            this._message = Resources.AuctionHouseRemoveInvalid;
            return;
        }

        var state = await this.AuthenticationStateProvider.GetAuthenticationStateAsync();
        var administrator = state.User.Identity?.Name ?? "unknown";
        await this.RunAsync(async () =>
        {
            var outcome = await this.Administration.RemoveAsync(listingId, administrator, this._reason.Trim());
            this._message = outcome is { } result ? string.Format(Resources.AuctionHouseRemoveOutcome, result) : null;
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        this._busy = true;
        try
        {
            await action();
        }
        finally
        {
            this._busy = false;
        }
    }
}
