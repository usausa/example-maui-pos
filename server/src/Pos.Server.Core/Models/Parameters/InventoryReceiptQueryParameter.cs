namespace Pos.Server.Models.Parameters;

// 入荷一覧の絞り込み (from / to は入荷予定日)
public sealed class InventoryReceiptQueryParameter : PagedParameter<InventoryReceiptSort>
{
    public Guid? StoreId { get; init; }

    public Guid? SupplierId { get; init; }

    public InventoryReceiptStatus? Status { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
