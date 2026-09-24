namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// 入荷の受領、移動の出荷・受領の確認 (担当と、受領なら届いた数)。文言は操作ごとにページが渡す
public sealed partial class InventoryMovementDialog
{
    private static readonly InventoryMovementFormValidator Validator = new();

    private List<StaffEntity> staffList = [];

    [Parameter]
    public required string Message { get; set; }

    [Parameter]
    public required string QuantityLabel { get; set; }

    [Parameter]
    public required string SubmitText { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        staffList = await StaffService.QueryAllAsync(false, CancellationToken.None);
    }
}
