namespace Pos.Contract.InventoryReceipts;

// 受領。明細を省略するか、含めない明細は予定の数で受け取る (数えた数が違うときだけ送ればよい)
public sealed class InventoryReceiptReceiveRequest
{
    public Guid? StaffId { get; set; }

    // 省略するとサーバの受付時刻
    public DateTime? ReceivedAt { get; set; }

    public IReadOnlyList<InventoryReceiptReceiveRequestLine> Lines { get; set; } = [];
}

public sealed class InventoryReceiptReceiveRequestLine
{
    public Guid LineId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }
}
