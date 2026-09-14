namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

public sealed partial class StaffEditDialog
{
    private static readonly StaffFormValidator Validator = new();

    private List<StoreEntity> stores = [];

    [Inject]
    public required StoreService StoreService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        stores = await StoreService.QueryAllAsync(false, CancellationToken.None);
    }
}
