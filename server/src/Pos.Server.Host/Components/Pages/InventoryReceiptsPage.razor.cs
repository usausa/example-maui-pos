namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Host.Application.State;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 入荷 (入荷予定の登録、受領で在庫に入れる、キャンセル)。受領は店舗の端末の検品でもできる
public sealed partial class InventoryReceiptsPage
{
    private MudDataGrid<InventoryReceiptDetailView> Grid { get; set; } = default!;

    private NameLookup names = new();
    private List<SupplierEntity> suppliers = [];
    private DateRange? period;
    private Guid? storeId;
    private Guid? supplierId;
    private InventoryReceiptStatus? status = InventoryReceiptStatus.Draft;

    [Inject]
    public required InventoryReceiptService InventoryReceiptService { get; set; }

    [Inject]
    public required SupplierService SupplierService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    // 在庫変動履歴から入荷を開く
    [SupplyParameterFromQuery(Name = "id")]
    public Guid? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        await LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
            suppliers = await SupplierService.QueryListAsync(true, CancellationToken);
        });
        if (Id is not null)
        {
            await ShowReceiptAsync(Id.Value);
        }
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<InventoryReceiptDetailView>> LoadServerData(GridState<InventoryReceiptDetailView> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new InventoryReceiptQueryParameter
        {
            StoreId = storeId,
            SupplierId = supplierId,
            Status = status,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Sort = EnumHelper.Parse(sort?.SortBy.Replace("Receipt.", string.Empty, StringComparison.Ordinal), InventoryReceiptSort.CreatedAt),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await InventoryReceiptService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<InventoryReceiptDetailView> { TotalItems = result.Total, Items = result.Items };
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task OnRowClick(DataGridRowClickEventArgs<InventoryReceiptDetailView> args) => ShowReceiptAsync(args.Item.Receipt.Id);

    //--------------------------------------------------------------------------------
    // Receipt
    //--------------------------------------------------------------------------------

    // 詳細で選んだ操作を実行する
    private async Task ShowReceiptAsync(Guid id)
    {
        var reference = await DialogService.ShowAsync<InventoryReceiptDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(InventoryReceiptDialog.Id), id },
                { nameof(InventoryReceiptDialog.Names), names }
            },
            Styles.LargeDialog);
        var result = await reference.Result;
        if (result is not { Canceled: false, Data: InventoryReceiptDialogResult selected })
        {
            return;
        }

        if (selected.Action == InventoryReceiptDialogAction.Receive)
        {
            await ReceiveAsync(selected.Receipt);
        }
        else
        {
            await CancelAsync(selected.Receipt);
        }
    }

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<InventoryReceiptEditDialog, InventoryReceiptForm>("入荷予定の登録", new InventoryReceiptForm { StoreId = storeId }, Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryReceiptService.CreateAsync(InventoryReceiptForm.ToEntity(form), InventoryReceiptForm.ToLines(form), CancellationToken), "登録しました。"), SearchAsync);
    }

    // 届いた数を確かめて受領する (予定と違う数は差として残る)
    private async Task ReceiveAsync(InventoryReceiptDetailView detail)
    {
        var form = await ShowEditDialogAsync<InventoryMovementDialog, InventoryMovementForm>(
            $"入荷の受領 {detail.SupplierName}",
            InventoryMovementForm.FromReceipt(detail),
            Styles.MediumDialog,
            static x =>
            {
                x.Add(nameof(InventoryMovementDialog.Message), "届いた数を確かめてください。受領すると届いた数が在庫に入ります。");
                x.Add(nameof(InventoryMovementDialog.QuantityLabel), "予定");
                x.Add(nameof(InventoryMovementDialog.SubmitText), "受領");
            });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryReceiptService.ReceiveAsync(detail.Receipt.Id, form.StaffId, null, InventoryMovementForm.ToQuantities(form), CancellationToken), "受領しました。"), SearchAsync);
    }

    private async Task CancelAsync(InventoryReceiptDetailView detail)
    {
        if (!await DialogService.ShowConfirm("入荷のキャンセル", $"{detail.SupplierName} の入荷予定をキャンセルしますか？"))
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryReceiptService.CancelAsync(detail.Receipt.Id, CancellationToken), "キャンセルしました。"), SearchAsync);
    }

    // 書き込みの結果 (業務ルール違反は API と同じ文言)
    private void Notify(InventoryReceiptResult result, string success)
    {
        switch (result.Status)
        {
            case InventoryReceiptResultStatus.Success:
                Snackbar.AddSuccess(success);
                break;
            case InventoryReceiptResultStatus.NotFound:
                Snackbar.AddError("対象が存在しません。");
                break;
            default:
                Snackbar.AddWarning($"{ApiRuleText.Of(result.Violation!.Reason)}。");
                break;
        }
    }
}
