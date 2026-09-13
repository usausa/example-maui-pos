namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-62 ポイント調整
public sealed partial class PointAdjustDialog
{
    private static readonly PointAdjustFormValidator Validator = new();

    private List<StaffEntity> staffList = [];

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        staffList = await StaffAccessor.QueryListAsync(null, null, false, "Code", ApiHelper.MaxPageSize, 0, CancellationToken.None);
    }
}
