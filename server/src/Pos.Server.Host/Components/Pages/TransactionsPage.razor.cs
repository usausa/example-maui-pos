namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Host.Application.State;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 取引一覧
public sealed partial class TransactionsPage
{
    private MudDataGrid<TransactionEntity> Grid { get; set; } = default!;

    private NameLookup names = new();
    private DateRange? period;
    private Guid? storeId;
    private Guid? terminalId;
    private TransactionType? type;
    private TransactionStatus? status;
    private string? receiptNo;
    private string? serialNumber;

    [Inject]
    public required TransactionService TransactionService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required PaymentMethodService PaymentMethodService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    // 取引 ID 指定で詳細を開く (顧客詳細などからのリンク)
    [SupplyParameterFromQuery(Name = "id")]
    public Guid? Id { get; set; }

    // シフト詳細からの絞り込み
    [SupplyParameterFromQuery(Name = "shiftId")]
    public Guid? ShiftId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        await LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, PaymentMethodService, CancellationToken);
        });
        if (Id is not null)
        {
            await ShowDetailAsync(Id.Value);
        }
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<TransactionEntity>> LoadServerData(GridState<TransactionEntity> state, CancellationToken cancellationToken)
    {
        // レシート番号は完全一致で 1 件
        if (!String.IsNullOrWhiteSpace(receiptNo))
        {
            var entity = await TransactionService.QueryByReceiptNoAsync(receiptNo.Trim(), cancellationToken);
            return new GridData<TransactionEntity> { TotalItems = entity is null ? 0 : 1, Items = entity is null ? [] : [entity] };
        }

        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new TransactionQueryParameter
        {
            StoreId = storeId,
            TerminalId = terminalId,
            ShiftId = ShiftId,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Type = type,
            Status = status,
            SerialNumber = serialNumber,
            Sort = EnumHelper.Parse(sort?.SortBy, TransactionSort.TransactedAt),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await TransactionService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<TransactionEntity> { TotalItems = result.Total, Items = result.Items };
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnSearchKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        terminalId = null;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task ClearShiftAsync()
    {
        Navigation.NavigateTo("transactions");
        ShiftId = null;
        return SearchAsync();
    }

    private Task OnRowClick(DataGridRowClickEventArgs<TransactionEntity> args) => ShowDetailAsync(args.Item.Id);

    private async Task ShowDetailAsync(Guid id)
    {
        var reference = await DialogService.ShowAsync<TransactionDetailDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(TransactionDetailDialog.Id), id },
                { nameof(TransactionDetailDialog.Names), names }
            },
            Styles.LargeDialog);
        await reference.Result;
    }
}
