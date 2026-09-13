namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Models.Entity;

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
    public required InventoryAccessor InventoryAccessor { get; set; }

    [Inject]
    public required ProductAccessor ProductAccessor { get; set; }

    [Inject]
    public required AdjustmentReasonAccessor AdjustmentReasonAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

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
            names = await NameLookup.LoadAsync(StoreAccessor, TerminalAccessor, StaffAccessor, null, CancellationToken);
            products = (await ProductAccessor.QueryListAsync(null, null, null, null, true, "Code", ApiHelper.MaxPageSize, 0, CancellationToken)).ToDictionary(static x => x.Id);
            reasons = (await AdjustmentReasonAccessor.QueryListAsync(null, true, CancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
            if ((ProductId is not null) && products.TryGetValue(ProductId.Value, out var selected))
            {
                product = selected;
            }
        });
    }

    private string ProductName(Guid id) => products.TryGetValue(id, out var x) ? $"{x.Code} {x.Name}" : "-";

    private string ReasonName(Guid? id) => (id is not null) && reasons.TryGetValue(id.Value, out var name) ? name : string.Empty;

    private async Task<GridData<InventoryChangeEntity>> LoadServerData(GridState<InventoryChangeEntity> state, CancellationToken cancellationToken)
    {
        // 期間は UTC 日時 (to は翌日 0 時未満)
        var from = period?.Start?.ToUniversalTime();
        var to = period?.End?.AddDays(1).ToUniversalTime();
        var total = await InventoryAccessor.CountChangesAsync(storeId, product?.Id, type, from, to, cancellationToken);
        var items = await InventoryAccessor.QueryChangeListAsync(storeId, product?.Id, type, from, to, state.PageSize, state.Page * state.PageSize, cancellationToken);
        return new GridData<InventoryChangeEntity> { TotalItems = (int)total, Items = items };
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
