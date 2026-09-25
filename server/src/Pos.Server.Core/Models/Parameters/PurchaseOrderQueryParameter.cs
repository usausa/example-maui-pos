namespace Pos.Server.Models.Parameters;

// 発注一覧の絞り込み (from / to は希望納期)
public sealed class PurchaseOrderQueryParameter : PagedParameter<PurchaseOrderSort>
{
    public Guid? StoreId { get; init; }

    public Guid? SupplierId { get; init; }

    public PurchaseOrderStatus? Status { get; init; }

    // 未完了 (下書き・発注済み) だけ
    public bool OpenOnly { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
