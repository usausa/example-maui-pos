namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 棚卸・調整登録
public sealed partial class InventoryChangeDialog
{
    private const int SearchLimit = 20;

    private static readonly InventoryChangeFormValidator Validator = new();

    private List<AdjustmentReasonEntity> reasons = [];
    private List<StaffEntity> staffList = [];

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required AdjustmentReasonService AdjustmentReasonService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        reasons = await AdjustmentReasonService.QueryListAsync(null, false, CancellationToken.None);
        staffList = await StaffService.QueryAllAsync(false, CancellationToken.None);
    }

    // 在庫管理対象の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var result = await ProductService.QueryPageAsync(new ProductQueryParameter { Keyword = value, IsActive = true, Size = SearchLimit }, cancellationToken);
        return result.Items.Where(static x => x.TrackInventory);
    }
}
