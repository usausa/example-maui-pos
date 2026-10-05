namespace Pos.Contract.Sync;

using Pos.Contract.AdjustmentReasons;
using Pos.Contract.Categories;
using Pos.Contract.Discounts;
using Pos.Contract.PaymentMethods;
using Pos.Contract.Products;
using Pos.Contract.Settings;
using Pos.Contract.Staff;
using Pos.Contract.Stores;
using Pos.Contract.TaxRates;
using Pos.Contract.Terminals;

// since 以降に更新されたマスタ。論理削除済みも isDeleted: true で含む
public sealed class SyncMastersResponse
{
    // 次回の since に使う
    public DateTime ServerTime { get; set; }

    // 変更があるときのみ
    public SettingsResponse? Settings { get; set; }

    public IReadOnlyList<StoreListResponseItem> Stores { get; set; } = default!;

    public IReadOnlyList<TerminalListResponseItem> Terminals { get; set; } = default!;

    public IReadOnlyList<StaffListResponseItem> Staff { get; set; } = default!;

    public IReadOnlyList<CategoryListResponseItem> Categories { get; set; } = default!;

    public IReadOnlyList<TaxRateListResponseItem> TaxRates { get; set; } = default!;

    public IReadOnlyList<ProductListResponseItem> Products { get; set; } = default!;

    public IReadOnlyList<DiscountListResponseItem> Discounts { get; set; } = default!;

    public IReadOnlyList<PaymentMethodListResponseItem> PaymentMethods { get; set; } = default!;

    public IReadOnlyList<AdjustmentReasonListResponseItem> AdjustmentReasons { get; set; } = default!;

    // 商品が多く products を省いたときに true (GET /products?updatedSince で分割取得する)
    public bool ProductsTruncated { get; set; }
}
