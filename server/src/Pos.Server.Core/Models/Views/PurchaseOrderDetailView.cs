namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 発注と明細 (仕入先の名前と、発注で作った入荷予定で受領した数付き)
public sealed class PurchaseOrderDetailView
{
    public required PurchaseOrderEntity PurchaseOrder { get; init; }

    public required string SupplierName { get; init; }

    public required IReadOnlyList<PurchaseOrderLineEntity> Lines { get; init; }

    // 明細番号ごとの受領した数 (受領の前は空)
    public IReadOnlyDictionary<int, decimal> ReceivedQuantities { get; init; } = new Dictionary<int, decimal>();

    // 数量 × 仕入単価の合計 (単価のない明細は含めない)
    public decimal TotalCost => Lines.Sum(static x => x.Quantity * (x.Cost ?? 0m));

    public decimal? ReceivedQuantityOf(PurchaseOrderLineEntity line) =>
        ReceivedQuantities.TryGetValue(line.LineNo, out var quantity) ? quantity : null;
}
