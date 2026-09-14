namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

public sealed partial class ProductEditDialog
{
    private static readonly ProductFormValidator Validator = new();

    private List<CategoryEntity> categories = [];
    private List<TaxRateEntity> taxRates = [];

    [Inject]
    public required CategoryService CategoryService { get; set; }

    [Inject]
    public required TaxRateService TaxRateService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        categories = await CategoryService.QueryAllAsync(false, CancellationToken.None);
        taxRates = await TaxRateService.QueryListAsync(null, false, CancellationToken.None);
    }
}
