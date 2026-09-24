namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

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

// 受注一覧 (取り寄せ・取り置き)。詳細から入荷・変更・キャンセルし、完了した受注は会計した取引を開く
public sealed partial class OrdersPage
{
    private enum StatusFilter
    {
        Open,
        Ordered,
        Arrived,
        Completed,
        Cancelled,
        All
    }

    private MudDataGrid<OrderDetailView> Grid { get; set; } = default!;

    private NameLookup names = new();
    private DateRange? period;
    private Guid? storeId;
    private StatusFilter statusFilter = StatusFilter.Open;
    private OrderType? type;
    private string? keyword;

    [Inject]
    public required OrderService OrderService { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

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

    // 会員詳細・ダッシュボードから受注を開く
    [SupplyParameterFromQuery(Name = "id")]
    public Guid? Id { get; set; }

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        await LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, PaymentMethodService, CancellationToken);
        });
        if (Id is not null)
        {
            await ShowOrderAsync(Id.Value);
        }
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<OrderDetailView>> LoadServerData(GridState<OrderDetailView> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new OrderQueryParameter
        {
            StoreId = storeId,
            Status = ToStatus(statusFilter),
            OpenOnly = statusFilter == StatusFilter.Open,
            Type = type,
            Keyword = keyword,
            From = ToDateOnly(period?.Start),
            To = ToDateOnly(period?.End),
            Sort = EnumHelper.Parse(sort?.SortBy.Replace("Order.", string.Empty, StringComparison.Ordinal), OrderSort.OrderedAt),
            Desc = sort?.Descending ?? true,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await OrderService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<OrderDetailView> { TotalItems = result.Total, Items = result.Items };
    }

    private static OrderStatus? ToStatus(StatusFilter filter) => filter switch
    {
        StatusFilter.Ordered => OrderStatus.Ordered,
        StatusFilter.Arrived => OrderStatus.Arrived,
        StatusFilter.Completed => OrderStatus.Completed,
        StatusFilter.Cancelled => OrderStatus.Cancelled,
        _ => null
    };

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnSearchKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task OnRowClick(DataGridRowClickEventArgs<OrderDetailView> args) => ShowOrderAsync(args.Item.Order.Id);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 詳細で選んだ操作を実行する
    private async Task ShowOrderAsync(Guid id)
    {
        var reference = await DialogService.ShowAsync<OrderDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(OrderDialog.Id), id },
                { nameof(OrderDialog.Names), names }
            },
            Styles.LargeDialog);
        var result = await reference.Result;
        if (result is not { Canceled: false, Data: OrderDialogResult selected })
        {
            return;
        }

        var order = selected.Order.Order;
        switch (selected.Action)
        {
            case OrderDialogAction.Edit:
                await EditAsync(selected.Order);
                break;
            case OrderDialogAction.Arrive:
                await RunAsync(async () => Notify(await OrderService.ArriveAsync(order.Id, CancellationToken), $"{order.OrderNo} を引き渡し待ちにしました。"), SearchAsync);
                break;
            case OrderDialogAction.Cancel:
                await CancelAsync(selected.Order);
                break;
            default:
                await ShowTransactionAsync(order.TransactionId!.Value);
                break;
        }
    }

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<OrderEditDialog, OrderForm>("受注の登録", new OrderForm { StoreId = storeId }, Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await OrderService.CreateAsync(OrderForm.ToDetail(form, Guid.CreateVersion7()), CancellationToken), "登録しました。"), SearchAsync);
    }

    private async Task EditAsync(OrderDetailView detail)
    {
        var customer = detail.Order.CustomerId is null ? null : await CustomerService.QueryAsync(detail.Order.CustomerId.Value, CancellationToken);
        var form = await ShowEditDialogAsync<OrderEditDialog, OrderForm>($"受注の変更 {detail.Order.OrderNo}", OrderForm.FromDetail(detail, customer), Styles.LargeDialog);
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => Notify(await OrderService.UpdateAsync(detail.Order.Id, OrderForm.ToParameter(form), CancellationToken), "変更しました。"), SearchAsync);
    }

    private async Task CancelAsync(OrderDetailView detail)
    {
        if (!await DialogService.ShowConfirm("受注のキャンセル", $"{detail.Order.OrderNo} ({detail.Order.CustomerName}) をキャンセルしますか？"))
        {
            return;
        }

        await RunAsync(async () => Notify(await OrderService.CancelAsync(detail.Order.Id, null, CancellationToken), "キャンセルしました。"), SearchAsync);
    }

    private async Task ShowTransactionAsync(Guid transactionId)
    {
        var reference = await DialogService.ShowAsync<TransactionDetailDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(TransactionDetailDialog.Id), transactionId },
                { nameof(TransactionDetailDialog.Names), names }
            },
            Styles.LargeDialog);
        await reference.Result;
    }

    // 書き込みの結果 (業務ルール違反は API と同じ文言)
    private void Notify(OrderResult result, string success)
    {
        switch (result.Status)
        {
            case OrderResultStatus.Success:
                Snackbar.AddSuccess(success);
                break;
            case OrderResultStatus.NotFound:
                Snackbar.AddError("対象が存在しません。");
                break;
            case OrderResultStatus.VersionMismatch:
                Snackbar.AddError("他で更新されています。再読み込みしてください。");
                break;
            case OrderResultStatus.Violation:
                Snackbar.AddWarning($"{ApiRuleText.Of(result.Violation!.Reason)}。");
                break;
            default:
                Snackbar.AddError("同じ受注が登録済みです。");
                break;
        }
    }
}
