namespace Pos.Terminal.Usecases;

using Pos.Contract.InventoryReceipts;
using Pos.Contract.InventoryTransfers;

// 入荷・移動の応答 → 受領待ちの伝票、数えた数 → 受領の要求
public static class ReceivingMapper
{
    public static ReceivingDocument FromReceipt(InventoryReceiptResponseItem receipt) =>
        new(
            ReceivingKind.Receipt,
            receipt.Id,
            receipt.SupplierName,
            receipt.SlipNo,
            receipt.ExpectedDate,
            null,
            receipt.Note,
            receipt.Lines.Select(static x => new ReceivingLine(x.Id, x.ProductId, x.ProductCode, x.ProductName, x.Quantity)).ToList());

    public static ReceivingDocument FromTransfer(InventoryTransferResponseItem transfer) =>
        new(
            ReceivingKind.Transfer,
            transfer.Id,
            transfer.FromStoreName,
            transfer.TransferNo,
            null,
            transfer.ShippedAt,
            transfer.Note,
            transfer.Lines.Select(static x => new ReceivingLine(x.Id, x.ProductId, x.ProductCode, x.ProductName, x.Quantity)).ToList());

    // 数えた明細だけ送る (送らない明細はサーバが予定・出荷の数で受け取る)
    public static InventoryReceiptReceiveRequest ToReceiptRequest(Guid? staffId, IReadOnlyDictionary<Guid, decimal> counts) =>
        new()
        {
            StaffId = staffId,
            Lines = counts.Select(static x => new InventoryReceiptReceiveRequestLine { LineId = x.Key, Quantity = x.Value }).ToList()
        };

    public static InventoryTransferReceiveRequest ToTransferRequest(Guid? staffId, IReadOnlyDictionary<Guid, decimal> counts) =>
        new()
        {
            StaffId = staffId,
            Lines = counts.Select(static x => new InventoryTransferReceiveRequestLine { LineId = x.Key, Quantity = x.Value }).ToList()
        };
}
