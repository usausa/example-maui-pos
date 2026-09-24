namespace Pos.Server.Models.Parameters;

// 店舗間移動一覧の絞り込み (StoreId は出荷店か入荷店のどちらか)
public sealed class InventoryTransferQueryParameter : PagedParameter<InventoryTransferSort>
{
    public Guid? StoreId { get; init; }

    public Guid? FromStoreId { get; init; }

    public Guid? ToStoreId { get; init; }

    public InventoryTransferStatus? Status { get; init; }

    // 未受領 (依頼・出荷済み) だけ
    public bool OpenOnly { get; init; }
}
