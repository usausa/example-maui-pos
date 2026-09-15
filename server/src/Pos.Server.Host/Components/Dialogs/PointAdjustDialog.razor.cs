namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// ポイント調整
public sealed partial class PointAdjustDialog
{
    private static readonly PointAdjustFormValidator Validator = new();

    private List<StaffEntity> staffList = [];

    [Inject]
    public required StaffService StaffService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        staffList = await StaffService.QueryAllAsync(false, CancellationToken.None);
    }
}
