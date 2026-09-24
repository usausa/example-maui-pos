namespace Pos.Server.Host.Models.Forms;

using System.Collections.ObjectModel;

using Pos.Server.Models.Entity;

// 入荷予定の登録 (受領で在庫に入る)
public sealed class InventoryReceiptForm
{
    public Guid? StoreId { get; set; }

    public Guid? SupplierId { get; set; }

    // 仕入先の納品書番号
    public string? SlipNo { get; set; }

    public DateTime? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public Collection<InventoryReceiptFormLine> Lines { get; } = [];

    // 登録の内容 (Id・状態・明細の写しはサービスが設定する)
    public static InventoryReceiptEntity ToEntity(InventoryReceiptForm form) =>
        new()
        {
            StoreId = form.StoreId ?? Guid.Empty,
            SupplierId = form.SupplierId ?? Guid.Empty,
            SlipNo = form.SlipNo,
            ExpectedDate = form.ExpectedDate is null ? null : DateOnly.FromDateTime(form.ExpectedDate.Value),
            Note = form.Note
        };

    public static IReadOnlyList<InventoryReceiptLineEntity> ToLines(InventoryReceiptForm form) =>
        form.Lines.Select(static x => new InventoryReceiptLineEntity
        {
            ProductId = x.Product?.Id ?? Guid.Empty,
            Quantity = x.Quantity,
            Cost = x.Cost
        }).ToList();
}

public sealed class InventoryReceiptFormLine
{
    public ProductEntity? Product { get; set; }

    public decimal Quantity { get; set; } = 1m;

    // 仕入単価 (省略できる)
    public decimal? Cost { get; set; }
}
