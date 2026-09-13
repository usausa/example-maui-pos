namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

public sealed partial class StaffEditDialog
{
    private static readonly StaffFormValidator Validator = new();

    private List<StoreEntity> stores = [];

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        stores = await StoreAccessor.QueryListAsync(null, false, "Code", ApiHelper.MaxPageSize, 0, CancellationToken.None);
    }
}
