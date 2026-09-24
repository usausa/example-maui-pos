namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Host.Application.State;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 店舗間移動 (依頼の登録、出荷で出荷店の在庫を減らす、受領で入荷店の在庫を増やす、キャンセル)。受領は入荷店の端末でもできる
public sealed partial class InventoryTransfersPage
{
    private enum StatusFilter
    {
        Open,
        Requested,
        Shipped,
        Received,
        Cancelled,
        All
    }

    private MudDataGrid<InventoryTransferDetailView> Grid { get; set; } = default!;

    private NameLookup names = new();
    private Guid? storeId;
    private StatusFilter statusFilter = StatusFilter.Open;

    [Inject]
    public required InventoryTransferService InventoryTransferService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    // 在庫変動履歴・ダッシュボードから移動を開く
    [SupplyParameterFromQuery(Name = "id")]
    public Guid? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        await LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
        });
        if (Id is not null)
        {
            await ShowTransferAsync(Id.Value);
        }
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<InventoryTransferDetailView>> LoadServerData(GridState<InventoryTransferDetailView> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new InventoryTransferQueryParameter
        {
            StoreId = storeId,
            Status = ToStatus(statusFilter),
            OpenOnly = statusFilter == StatusFilter.Open,
            Sort = EnumHelper.Parse(sort?.SortBy.Replace("Transfer.", string.Empty, StringComparison.Ordinal), InventoryTransferSort.CreatedAt),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await InventoryTransferService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<InventoryTransferDetailView> { TotalItems = result.Total, Items = result.Items };
    }

    private static InventoryTransferStatus? ToStatus(StatusFilter filter) => filter switch
    {
        StatusFilter.Requested => InventoryTransferStatus.Requested,
        StatusFilter.Shipped => InventoryTransferStatus.Shipped,
        StatusFilter.Received => InventoryTransferStatus.Received,
        StatusFilter.Cancelled => InventoryTransferStatus.Cancelled,
        _ => null
    };

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task OnRowClick(DataGridRowClickEventArgs<InventoryTransferDetailView> args) => ShowTransferAsync(args.Item.Transfer.Id);

    //--------------------------------------------------------------------------------
    // Transfer
    //--------------------------------------------------------------------------------

    // 詳細で選んだ操作を実行する
    private async Task ShowTransferAsync(Guid id)
    {
        var reference = await DialogService.ShowAsync<InventoryTransferDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(InventoryTransferDialog.Id), id },
                { nameof(InventoryTransferDialog.Names), names }
            },
            Styles.LargeDialog);
        var result = await reference.Result;
        if (result is not { Canceled: false, Data: InventoryTransferDialogResult selected })
        {
            return;
        }

        switch (selected.Action)
        {
            case InventoryTransferDialogAction.Ship:
                await ShipAsync(selected.Transfer);
                break;
            case InventoryTransferDialogAction.Receive:
                await ReceiveAsync(selected.Transfer);
                break;
            default:
                await CancelAsync(selected.Transfer);
                break;
        }
    }

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<InventoryTransferEditDialog, InventoryTransferForm>("店舗間移動の依頼", new InventoryTransferForm { FromStoreId = storeId }, Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryTransferService.CreateAsync(form.FromStoreId!.Value, form.ToStoreId!.Value, form.Note, InventoryTransferForm.ToLines(form), CancellationToken), "依頼しました。"), SearchAsync);
    }

    // 依頼の数で出荷する (出荷店の在庫が減る)
    private async Task ShipAsync(InventoryTransferDetailView detail)
    {
        var form = await ShowEditDialogAsync<InventoryMovementDialog, InventoryMovementForm>(
            $"移動の出荷 {detail.Transfer.TransferNo}",
            InventoryMovementForm.FromTransfer(detail, false),
            Styles.MediumDialog,
            x =>
            {
                x.Add(nameof(InventoryMovementDialog.Message), $"依頼の数で出荷します。{names.Store(detail.Transfer.FromStoreId)} の在庫が減ります。");
                x.Add(nameof(InventoryMovementDialog.QuantityLabel), "数量");
                x.Add(nameof(InventoryMovementDialog.SubmitText), "出荷");
            });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryTransferService.ShipAsync(detail.Transfer.Id, form.StaffId, null, CancellationToken), "出荷しました。"), SearchAsync);
    }

    // 届いた数を確かめて受領する (出荷と違う数は差として残る)
    private async Task ReceiveAsync(InventoryTransferDetailView detail)
    {
        var form = await ShowEditDialogAsync<InventoryMovementDialog, InventoryMovementForm>(
            $"移動の受領 {detail.Transfer.TransferNo}",
            InventoryMovementForm.FromTransfer(detail, true),
            Styles.MediumDialog,
            x =>
            {
                x.Add(nameof(InventoryMovementDialog.Message), $"届いた数を確かめてください。受領すると届いた数が {names.Store(detail.Transfer.ToStoreId)} の在庫に入ります。");
                x.Add(nameof(InventoryMovementDialog.QuantityLabel), "出荷");
                x.Add(nameof(InventoryMovementDialog.SubmitText), "受領");
            });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryTransferService.ReceiveAsync(detail.Transfer.Id, form.StaffId, null, InventoryMovementForm.ToQuantities(form), CancellationToken), "受領しました。"), SearchAsync);
    }

    private async Task CancelAsync(InventoryTransferDetailView detail)
    {
        if (!await DialogService.ShowConfirm("移動のキャンセル", $"{detail.Transfer.TransferNo} の依頼をキャンセルしますか？"))
        {
            return;
        }

        await RunAsync(async () => Notify(await InventoryTransferService.CancelAsync(detail.Transfer.Id, CancellationToken), "キャンセルしました。"), SearchAsync);
    }

    // 書き込みの結果 (業務ルール違反は API と同じ文言)
    private void Notify(InventoryTransferResult result, string success)
    {
        switch (result.Status)
        {
            case InventoryTransferResultStatus.Success:
                Snackbar.AddSuccess(success);
                break;
            case InventoryTransferResultStatus.NotFound:
                Snackbar.AddError("対象が存在しません。");
                break;
            default:
                Snackbar.AddWarning($"{ApiRuleText.Of(result.Violation!.Reason)}。");
                break;
        }
    }
}
