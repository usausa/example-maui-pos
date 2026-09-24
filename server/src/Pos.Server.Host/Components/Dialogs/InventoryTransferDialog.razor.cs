namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Views;
using Pos.Server.Services;

public enum InventoryTransferDialogAction
{
    Ship,
    Receive,
    Cancel
}

// 店舗間移動の詳細で選んだ操作 (実行はページが行う)
public sealed record InventoryTransferDialogResult(InventoryTransferDialogAction Action, InventoryTransferDetailView Transfer);

// 店舗間移動の詳細 (出荷店・入荷店・経過・明細)。依頼なら [出荷] [キャンセル]、出荷済みなら [受領] を出す
public sealed partial class InventoryTransferDialog
{
    private InventoryTransferDetailView? detail;

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public required NameLookup Names { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required InventoryTransferService InventoryTransferService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        detail = await InventoryTransferService.QueryDetailAsync(Id, CancellationToken.None);
    }

    private void Close(InventoryTransferDialogAction action) => MudDialog.Close(DialogResult.Ok(new InventoryTransferDialogResult(action, detail!)));
}
