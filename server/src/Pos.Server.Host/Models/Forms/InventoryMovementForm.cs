namespace Pos.Server.Host.Models.Forms;

using System.Collections.ObjectModel;

using Pos.Server.Models.Views;

// 入荷・移動の出荷と受領の確認。受領は明細ごとに届いた数を直せる (出荷は依頼の数で出す)
public sealed class InventoryMovementForm
{
    public bool QuantityEditable { get; init; }

    public Guid? StaffId { get; set; }

    public Collection<InventoryMovementFormLine> Lines { get; } = [];

    public static InventoryMovementForm FromReceipt(InventoryReceiptDetailView detail) =>
        Create(true, detail.Lines.Select(static x => (x.Id, x.ProductCode, x.ProductName, x.Quantity)));

    public static InventoryMovementForm FromTransfer(InventoryTransferDetailView detail, bool receive) =>
        Create(receive, detail.Lines.Select(static x => (x.Id, x.ProductCode, x.ProductName, x.Quantity)));

    // 受領の数 (明細 ID → 届いた数)
    public static Dictionary<Guid, decimal> ToQuantities(InventoryMovementForm form) =>
        form.Lines.ToDictionary(static x => x.LineId, static x => x.ActualQuantity);

    // 届いた数の初期値は予定・出荷の数
    private static InventoryMovementForm Create(bool quantityEditable, IEnumerable<(Guid Id, string ProductCode, string ProductName, decimal Quantity)> lines)
    {
        var form = new InventoryMovementForm { QuantityEditable = quantityEditable };
        foreach (var (id, productCode, productName, quantity) in lines)
        {
            form.Lines.Add(new InventoryMovementFormLine { LineId = id, ProductCode = productCode, ProductName = productName, Quantity = quantity, ActualQuantity = quantity });
        }

        return form;
    }
}

public sealed class InventoryMovementFormLine
{
    public Guid LineId { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    // 予定 (入荷) か出荷 (移動) の数
    public decimal Quantity { get; init; }

    // 届いた数
    public decimal ActualQuantity { get; set; }
}
