namespace Pos.Server.Host.Models.Forms;

using System.Collections.ObjectModel;

using Pos.Server.Models.Entity;

// 店舗間移動の依頼 (出荷で出荷店の在庫が減り、受領で入荷店の在庫が増える)
public sealed class InventoryTransferForm
{
    public Guid? FromStoreId { get; set; }

    public Guid? ToStoreId { get; set; }

    public string? Note { get; set; }

    public Collection<InventoryTransferFormLine> Lines { get; } = [];

    // 明細 (Id・行番号・商品の写しはサービスが設定する)
    public static IReadOnlyList<InventoryTransferLineEntity> ToLines(InventoryTransferForm form) =>
        form.Lines.Select(static x => new InventoryTransferLineEntity
        {
            ProductId = x.Product?.Id ?? Guid.Empty,
            Quantity = x.Quantity
        }).ToList();
}

public sealed class InventoryTransferFormLine
{
    public ProductEntity? Product { get; set; }

    public decimal Quantity { get; set; } = 1m;
}
