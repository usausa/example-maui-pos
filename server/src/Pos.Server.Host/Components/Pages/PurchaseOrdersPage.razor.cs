namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

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

// 発注 (下書きの作成と変更、発注で入荷予定を作る、キャンセル、発注書 PDF)。受領は入荷の画面か店舗の端末の検品で行う
public sealed partial class PurchaseOrdersPage
{
    private enum StatusFilter
    {
        Open,
        Draft,
        Ordered,
        Received,
        Cancelled,
        All
    }

    private MudDataGrid<PurchaseOrderDetailView> Grid { get; set; } = default!;

    private NameLookup names = new();
    private List<SupplierEntity> suppliers = [];
    private DateRange? period;
    private Guid? storeId;
    private Guid? supplierId;
    private StatusFilter statusFilter = StatusFilter.Open;

    [Inject]
    public required PurchaseOrderService PurchaseOrderService { get; set; }

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

    [CascadingParameter]
    public required Task<AuthenticationState> AuthenticationState { get; set; }

    // 入荷予定から発注を開く
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
            await ShowPurchaseOrderAsync(Id.Value);
        }
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<PurchaseOrderDetailView>> LoadServerData(GridState<PurchaseOrderDetailView> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new PurchaseOrderQueryParameter
        {
            StoreId = storeId,
            SupplierId = supplierId,
            Status = ToStatus(statusFilter),
            OpenOnly = statusFilter == StatusFilter.Open,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Sort = EnumHelper.Parse(sort?.SortBy.Replace("PurchaseOrder.", string.Empty, StringComparison.Ordinal), PurchaseOrderSort.CreatedAt),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await PurchaseOrderService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<PurchaseOrderDetailView> { TotalItems = result.Total, Items = result.Items };
    }

    private static PurchaseOrderStatus? ToStatus(StatusFilter filter) => filter switch
    {
        StatusFilter.Draft => PurchaseOrderStatus.Draft,
        StatusFilter.Ordered => PurchaseOrderStatus.Ordered,
        StatusFilter.Received => PurchaseOrderStatus.Received,
        StatusFilter.Cancelled => PurchaseOrderStatus.Cancelled,
        _ => null
    };

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task OnRowClick(DataGridRowClickEventArgs<PurchaseOrderDetailView> args) => ShowPurchaseOrderAsync(args.Item.PurchaseOrder.Id);

    //--------------------------------------------------------------------------------
    // PurchaseOrder
    //--------------------------------------------------------------------------------

    // 詳細で選んだ操作を実行する
    private async Task ShowPurchaseOrderAsync(Guid id)
    {
        var reference = await DialogService.ShowAsync<PurchaseOrderDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(PurchaseOrderDialog.Id), id },
                { nameof(PurchaseOrderDialog.Names), names }
            },
            Styles.LargeDialog);
        var result = await reference.Result;
        if (result is not { Canceled: false, Data: PurchaseOrderDialogResult selected })
        {
            return;
        }

        switch (selected.Action)
        {
            case PurchaseOrderDialogAction.Edit:
                await EditAsync(selected.PurchaseOrder);
                break;
            case PurchaseOrderDialogAction.Order:
                await OrderAsync(selected.PurchaseOrder);
                break;
            default:
                await CancelAsync(selected.PurchaseOrder);
                break;
        }
    }

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<PurchaseOrderEditDialog, PurchaseOrderForm>("発注の作成", new PurchaseOrderForm { StoreId = storeId }, Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await PurchaseOrderService.CreateAsync(PurchaseOrderForm.ToEntity(form), PurchaseOrderForm.ToLines(form), CancellationToken), "下書きを作成しました。"), SearchAsync);
    }

    private async Task EditAsync(PurchaseOrderDetailView detail)
    {
        var form = await ShowEditDialogAsync<PurchaseOrderEditDialog, PurchaseOrderForm>($"発注の変更 {detail.PurchaseOrder.PurchaseOrderNo}", PurchaseOrderForm.FromDetail(detail), Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await PurchaseOrderService.UpdateAsync(detail.PurchaseOrder.Id, PurchaseOrderForm.ToParameter(form), CancellationToken), "変更しました。"), SearchAsync);
    }

    // 発注すると明細を写した入荷予定ができる (発注書は PDF で仕入先に送る)
    private async Task OrderAsync(PurchaseOrderDetailView detail)
    {
        if (!await DialogService.ShowConfirm("発注", $"{detail.PurchaseOrder.PurchaseOrderNo} ({detail.SupplierName}) を発注しますか？発注すると入荷予定ができ、内容は変えられなくなります。"))
        {
            return;
        }

        var account = AuthClaims.AccountOf((await AuthenticationState).User);
        await RunAsync(async () => Notify(await PurchaseOrderService.OrderAsync(detail.PurchaseOrder.Id, account?.Name, CancellationToken), "発注しました。入荷予定を作りました。"), SearchAsync);
    }

    private async Task CancelAsync(PurchaseOrderDetailView detail)
    {
        var message = detail.PurchaseOrder.Status == PurchaseOrderStatus.Ordered
            ? $"{detail.PurchaseOrder.PurchaseOrderNo} をキャンセルしますか？入荷予定もキャンセルします。"
            : $"{detail.PurchaseOrder.PurchaseOrderNo} をキャンセルしますか？";
        if (!await DialogService.ShowConfirm("発注のキャンセル", message))
        {
            return;
        }

        await RunAsync(async () => Notify(await PurchaseOrderService.CancelAsync(detail.PurchaseOrder.Id, CancellationToken), "キャンセルしました。"), SearchAsync);
    }

    // 書き込みの結果 (業務ルール違反は API と同じ文言)
    private void Notify(PurchaseOrderResult result, string success)
    {
        switch (result.Status)
        {
            case PurchaseOrderResultStatus.Success:
                Snackbar.AddSuccess(success);
                break;
            case PurchaseOrderResultStatus.NotFound:
                Snackbar.AddError("対象が存在しません。");
                break;
            case PurchaseOrderResultStatus.VersionMismatch:
                Snackbar.AddError("他で更新されています。再読み込みしてください。");
                break;
            default:
                Snackbar.AddWarning($"{ApiRuleText.Of(result.Violation!.Reason)}。");
                break;
        }
    }
}
