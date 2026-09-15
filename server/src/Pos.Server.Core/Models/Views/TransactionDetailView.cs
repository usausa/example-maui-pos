namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 取引一式 (明細・シリアル・値引・税・支払・配送)
public sealed class TransactionDetailView
{
    public required TransactionEntity Transaction { get; init; }

    public required IReadOnlyList<TransactionLineEntity> Lines { get; init; }

    public IReadOnlyList<TransactionLineSerialEntity> Serials { get; init; } = [];

    public IReadOnlyList<TransactionDiscountEntity> Discounts { get; init; } = [];

    public IReadOnlyList<TransactionTaxSummaryEntity> TaxSummaries { get; init; } = [];

    public IReadOnlyList<TransactionPaymentEntity> Payments { get; init; } = [];

    public TransactionDeliveryEntity? Delivery { get; init; }
}
