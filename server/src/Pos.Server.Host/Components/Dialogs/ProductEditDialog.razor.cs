namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

public sealed partial class ProductEditDialog
{
    private static readonly ProductFormValidator Validator = new();

    private List<CategoryEntity> categories = [];

    private List<TaxRateEntity> taxRates = [];

    [Inject]
    public required CategoryAccessor CategoryAccessor { get; set; }

    [Inject]
    public required TaxRateAccessor TaxRateAccessor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        categories = CategoryOrder.Sort(await CategoryAccessor.QueryListAsync(null, false, "SortOrder", ApiHelper.MaxPageSize, 0, CancellationToken.None));
        taxRates = await TaxRateAccessor.QueryListAsync(null, false, CancellationToken.None);
    }
}
