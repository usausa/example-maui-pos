namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Views;
using Pos.Server.Services;

public enum PurchaseOrderDialogAction
{
    Edit,
    Order,
    Cancel
}

// 発注の詳細で選んだ操作 (実行はページが行う)
public sealed record PurchaseOrderDialogResult(PurchaseOrderDialogAction Action, PurchaseOrderDetailView PurchaseOrder);

// 発注の詳細 (発注・経過・明細と入荷した数)。下書きは [変更] [発注]、未完了は [発注をキャンセル] を出す
public sealed partial class PurchaseOrderDialog
{
    private PurchaseOrderDetailView? detail;

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required PurchaseOrderService PurchaseOrderService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        detail = await PurchaseOrderService.QueryDetailAsync(Id, CancellationToken.None);
    }

    private void Close(PurchaseOrderDialogAction action) => MudDialog.Close(DialogResult.Ok(new PurchaseOrderDialogResult(action, detail!)));
}
