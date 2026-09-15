namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 端末のマスタ同期: since 以降に更新されたマスタ (論理削除済みも含む)。Settings は変更があるときのみ
public sealed class SyncMasterDataView
{
    public required DateTime ServerTime { get; init; }

    public SettingsEntity? Settings { get; init; }

    public required IReadOnlyList<StoreEntity> Stores { get; init; }

    public required IReadOnlyList<TerminalEntity> Terminals { get; init; }

    public required IReadOnlyList<StaffEntity> Staff { get; init; }

    public required IReadOnlyList<CategoryEntity> Categories { get; init; }

    public required IReadOnlyList<TaxRateEntity> TaxRates { get; init; }

    public required IReadOnlyList<ProductEntity> Products { get; init; }

    public required IReadOnlyList<DiscountEntity> Discounts { get; init; }

    public required IReadOnlyList<PaymentMethodEntity> PaymentMethods { get; init; }

    public required IReadOnlyList<AdjustmentReasonEntity> AdjustmentReasons { get; init; }
}
