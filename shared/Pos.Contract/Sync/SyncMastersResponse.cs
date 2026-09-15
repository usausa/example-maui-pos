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

    public IReadOnlyList<StoreResponseItem> Stores { get; set; } = default!;

    public IReadOnlyList<TerminalResponseItem> Terminals { get; set; } = default!;

    public IReadOnlyList<StaffResponseItem> Staff { get; set; } = default!;

    public IReadOnlyList<CategoryResponseItem> Categories { get; set; } = default!;

    public IReadOnlyList<TaxRateResponseItem> TaxRates { get; set; } = default!;

    public IReadOnlyList<ProductResponseItem> Products { get; set; } = default!;

    public IReadOnlyList<DiscountResponseItem> Discounts { get; set; } = default!;

    public IReadOnlyList<PaymentMethodResponseItem> PaymentMethods { get; set; } = default!;

    public IReadOnlyList<AdjustmentReasonResponseItem> AdjustmentReasons { get; set; } = default!;

    // 商品が多く products を省いたときに true (GET /products?updatedSince で分割取得する)
    public bool ProductsTruncated { get; set; }
}
