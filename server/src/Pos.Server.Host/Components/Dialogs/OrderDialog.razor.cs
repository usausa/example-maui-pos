namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Views;
using Pos.Server.Services;

public enum OrderDialogAction
{
    Edit,
    Arrive,
    Cancel,
    OpenTransaction
}

// 受注の詳細で選んだ操作 (実行はページが行う)
public sealed record OrderDialogResult(OrderDialogAction Action, OrderDetailView Order);

// 受注の詳細 (連絡先・経過・明細)。状態に合わせて [入荷] [変更] [キャンセル] [会計した取引] を出す
public sealed partial class OrderDialog
{
    private OrderDetailView? detail;

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required OrderService OrderService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        detail = await OrderService.QueryDetailAsync(Id, CancellationToken.None);
    }

    private void Close(OrderDialogAction action) => MudDialog.Close(DialogResult.Ok(new OrderDialogResult(action, detail!)));
}
