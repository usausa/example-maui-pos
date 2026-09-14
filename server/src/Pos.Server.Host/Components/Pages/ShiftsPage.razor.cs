namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// S-30 シフト一覧
public sealed partial class ShiftsPage
{
    private MudDataGrid<ShiftDetail> Grid { get; set; } = default!;

    private NameLookup names = new();
    private DateRange? period;
    private Guid? storeId;
    private Guid? terminalId;
    private ShiftStatus? status;

    [Inject]
    public required ShiftService ShiftService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        return LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
        });
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    // Open 中は取引から都度集計し、Closed は確定値
    private async Task<GridData<ShiftDetail>> LoadServerData(GridState<ShiftDetail> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new ShiftQueryParameter
        {
            StoreId = storeId,
            TerminalId = terminalId,
            Status = status,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Sort = sort?.SortBy.Replace("Shift.", string.Empty, StringComparison.Ordinal),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await ShiftService.QueryDetailPageAsync(parameter, cancellationToken);
        return new GridData<ShiftDetail> { TotalItems = result.Total, Items = result.Items };
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        terminalId = null;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private async Task OnRowClick(DataGridRowClickEventArgs<ShiftDetail> args)
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
}
