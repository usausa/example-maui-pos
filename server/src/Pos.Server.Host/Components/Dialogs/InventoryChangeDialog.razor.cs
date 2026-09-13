namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

using Smart.Data;

// S-43 棚卸・調整登録
public sealed partial class InventoryChangeDialog
{
    private const int SearchLimit = 20;

    private static readonly InventoryChangeFormValidator Validator = new();

    private List<AdjustmentReasonEntity> reasons = [];

    private List<StaffEntity> staffList = [];

    [Inject]
    public required ProductAccessor ProductAccessor { get; set; }

    [Inject]
    public required AdjustmentReasonAccessor AdjustmentReasonAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    [Inject]
    public required IDialect Dialect { get; set; }

    protected override async Task OnInitializedAsync()
    {
        reasons = await AdjustmentReasonAccessor.QueryListAsync(null, false, CancellationToken.None);
        staffList = await StaffAccessor.QueryListAsync(null, null, false, "Code", ApiHelper.MaxPageSize, 0, CancellationToken.None);
    }

    // 在庫管理対象の商品をコード / JAN / 名称 / かなで検索
    private async Task<IEnumerable<ProductEntity>> SearchProductsAsync(string? value, CancellationToken cancellationToken)
    {
        var products = await ProductAccessor.QueryListAsync(null, ApiHelper.ToLikePattern(Dialect, value), true, null, false, "Code", SearchLimit, 0, cancellationToken);
        return products.Where(static x => x.TrackInventory);
    }
}
