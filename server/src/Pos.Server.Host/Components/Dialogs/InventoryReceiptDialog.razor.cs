namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Views;
using Pos.Server.Services;

public enum InventoryReceiptDialogAction
{
    Receive,
    Cancel
}

// 入荷の詳細で選んだ操作 (実行はページが行う)
public sealed record InventoryReceiptDialogResult(InventoryReceiptDialogAction Action, InventoryReceiptDetailView Receipt);

// 入荷の詳細 (入荷先・経過・明細)。入荷予定のときだけ [受領] [キャンセル] を出す
public sealed partial class InventoryReceiptDialog
{
    private InventoryReceiptDetailView? detail;

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required InventoryReceiptService InventoryReceiptService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        detail = await InventoryReceiptService.QueryDetailAsync(Id, CancellationToken.None);
    }

    private void Close(InventoryReceiptDialogAction action) => MudDialog.Close(DialogResult.Ok(new InventoryReceiptDialogResult(action, detail!)));
}
