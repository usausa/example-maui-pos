namespace Pos.Shared.Sync;

using Pos.Shared.Categories;
using Pos.Shared.Discounts;
using Pos.Shared.Inventory;
using Pos.Shared.PaymentMethods;
using Pos.Shared.Products;
using Pos.Shared.Settings;
using Pos.Shared.Staff;
using Pos.Shared.Stores;
using Pos.Shared.TaxRates;
using Pos.Shared.Terminals;

// since 以降に更新されたマスタ (api-design §3.10)。論理削除済みも isDeleted: true で含む
public sealed class SyncMastersResponse
{
    // 次回の since に使う
    public DateTime ServerTime { get; set; }

    // 変更があるときのみ
    public SettingsResponse? Settings { get; set; }

    public IReadOnlyList<StoreResponse> Stores { get; set; } = default!;

    public IReadOnlyList<TerminalResponse> Terminals { get; set; } = default!;

    public IReadOnlyList<StaffResponse> Staff { get; set; } = default!;

    public IReadOnlyList<CategoryResponse> Categories { get; set; } = default!;

    public IReadOnlyList<TaxRateResponse> TaxRates { get; set; } = default!;

    public IReadOnlyList<ProductResponse> Products { get; set; } = default!;

    public IReadOnlyList<DiscountResponse> Discounts { get; set; } = default!;

    public IReadOnlyList<PaymentMethodResponse> PaymentMethods { get; set; } = default!;

    public IReadOnlyList<AdjustmentReasonResponse> AdjustmentReasons { get; set; } = default!;

    // 商品が多く products を省いたときに true (GET /products?updatedSince で分割取得する)
    public bool ProductsTruncated { get; set; }
}
