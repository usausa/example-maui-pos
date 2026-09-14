namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// S-42 在庫変動履歴
public sealed partial class InventoryChangesPage
{
    private const int SearchLimit = 20;

    private MudDataGrid<InventoryChangeEntity> Grid { get; set; } = default!;

    private NameLookup names = new();
    private Dictionary<Guid, ProductEntity> products = [];
    private Dictionary<Guid, string> reasons = [];
    private DateRange? period;
    private Guid? storeId;
    private ProductEntity? product;
    private InventoryChangeType? type;

    [Inject]
    public required InventoryService InventoryService { get; set; }

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required AdjustmentReasonService AdjustmentReasonService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    // 商品別全店在庫からのリンク
    [SupplyParameterFromQuery(Name = "productId")]
    public Guid? ProductId { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        return LoadAsync(async () =>
        {
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
            products = (await ProductService.QueryAllAsync(true, CancellationToken)).ToDictionary(static x => x.Id);
            reasons = (await AdjustmentReasonService.QueryListAsync(null, true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            if ((ProductId is not null) && products.TryGetValue(ProductId.Value, out var selected))
            {
                product = selected;
            }
        });
    }

    private string ProductName(Guid id) => products.TryGetValue(id, out var x) ? x.ToDisplayText() : "-";

    private string ReasonName(Guid? id) => (id is not null) && reasons.TryGetValue(id.Value, out var name) ? name : string.Empty;

    private async Task<GridData<InventoryChangeEntity>> LoadServerData(GridState<InventoryChangeEntity> state, CancellationToken cancellationToken)
    {
        // 期間は UTC 日時 (to は翌日 0 時未満)
        var parameter = new InventoryChangeQueryParameter
        {
            StoreId = storeId,
            ProductId = product?.Id,
            Type = type,
            From = period?.Start?.ToUniversalTime(),
            To = period?.End?.AddDays(1).ToUniversalTime(),
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await InventoryService.QueryChangePageAsync(parameter, cancellationToken);
        return new GridData<InventoryChangeEntity> { TotalItems = result.Total, Items = result.Items };
    }

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var keyword = value?.Trim() ?? string.Empty;
        return Task.FromResult(products.Values
            .Where(x => x.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) || x.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) || (x.Kana?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) || (x.Barcode?.Contains(keyword, StringComparison.Ordinal) ?? false))
            .OrderBy(static x => x.Code, StringComparer.Ordinal)
            .Take(SearchLimit));
    }
}
