namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Models.Entity;

// S-20 取引一覧
public sealed partial class TransactionsPage
{
    private static readonly string[] SortColumns = ["TransactedAt", "ReceiptNo", "Total", "BusinessDate"];

    private MudDataGrid<TransactionEntity> Grid { get; set; } = default!;

    private NameLookup names = new();

    private DateRange? period;

    private Guid? storeId;

    private Guid? terminalId;

    private TransactionType? type;

    private TransactionStatus? status;

    private string? receiptNo;

    [Inject]
    public required TransactionAccessor TransactionAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    [Inject]
    public required PaymentMethodAccessor PaymentMethodAccessor { get; set; }

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
            names = await NameLookup.LoadAsync(StoreAccessor, TerminalAccessor, StaffAccessor, PaymentMethodAccessor, CancellationToken);
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
        var sort = state.SortDefinitions.FirstOrDefault();
        var order = SqlHelper.NormalizeSort(SortColumns, "TransactedAt", sort?.SortBy, sort?.Descending ?? true);
        var from = ToDateOnly(period?.Start);
        var to = ToDateOnly(period?.End);

        // レシート番号は完全一致で 1 件
        if (!String.IsNullOrWhiteSpace(receiptNo))
        {
            var entity = await TransactionAccessor.QueryByReceiptNoAsync(receiptNo.Trim(), cancellationToken);
            return new GridData<TransactionEntity> { TotalItems = entity is null ? 0 : 1, Items = entity is null ? [] : [entity] };
        }

        var total = await TransactionAccessor.CountAsync(storeId, terminalId, null, ShiftId, null, from, to, type, status, cancellationToken);
        var items = await TransactionAccessor.QueryListAsync(storeId, terminalId, null, ShiftId, null, from, to, type, status, order, state.PageSize, state.Page * state.PageSize, cancellationToken);
        return new GridData<TransactionEntity> { TotalItems = (int)total, Items = items };
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
