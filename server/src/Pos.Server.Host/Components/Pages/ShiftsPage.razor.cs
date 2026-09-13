namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Mappers;
using Pos.Server.Models.Entity;

// S-30 シフト一覧
public sealed partial class ShiftsPage
{
    private static readonly string[] SortColumns = ["OpenedAt", "BusinessDate", "ClosedAt"];

    private MudDataGrid<ShiftRow> Grid { get; set; } = default!;

    private NameLookup names = new();

    private DateRange? period;

    private Guid? storeId;

    private Guid? terminalId;

    private ShiftStatus? status;

    [Inject]
    public required ShiftAccessor ShiftAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        return LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreAccessor, TerminalAccessor, StaffAccessor, null, CancellationToken);
        });
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    // Open 中は取引から都度集計し、Closed は確定値
    private async Task<GridData<ShiftRow>> LoadServerData(GridState<ShiftRow> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var order = SqlHelper.NormalizeSort(SortColumns, "OpenedAt", sort?.SortBy.Replace("Shift.", string.Empty, StringComparison.Ordinal), sort?.Descending ?? true);
        var from = period?.Start is null ? (DateOnly?)null : DateOnly.FromDateTime(period.Start.Value);
        var to = period?.End is null ? (DateOnly?)null : DateOnly.FromDateTime(period.End.Value);

        var total = await ShiftAccessor.CountAsync(storeId, terminalId, status, from, to, cancellationToken);
        var shifts = await ShiftAccessor.QueryListAsync(storeId, terminalId, status, from, to, order, state.PageSize, state.Page * state.PageSize, cancellationToken);
        var rows = new List<ShiftRow>(shifts.Count);
        foreach (var shift in shifts)
        {
            var totals = await ShiftMapper.ResolveTotalsAsync(ShiftAccessor, shift, cancellationToken);
            rows.Add(new ShiftRow(shift, ShiftMapper.ExpectedCash(shift.OpeningCash, totals), totals.SalesTotal, totals.SalesCount));
        }

        return new GridData<ShiftRow> { TotalItems = (int)total, Items = rows };
    }

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        terminalId = null;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private async Task OnRowClick(DataGridRowClickEventArgs<ShiftRow> args)
    {
        var reference = await DialogService.ShowAsync<ShiftDetailDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(ShiftDetailDialog.Id), args.Item.Shift.Id },
                { nameof(ShiftDetailDialog.Names), names }
            },
            Styles.LargeDialog);
        await reference.Result;
    }

    private sealed record ShiftRow(ShiftEntity Shift, decimal ExpectedCash, decimal SalesTotal, int SalesCount);
}
