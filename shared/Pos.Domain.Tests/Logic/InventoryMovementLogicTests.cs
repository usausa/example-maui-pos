namespace Pos.Domain.Logic;

// 入荷と店舗間移動の状態遷移
public sealed class InventoryMovementLogicTests
{
    // 入荷の受領・キャンセルは入荷予定のときだけ
    [Theory]
    [InlineData(InventoryReceiptStatus.Draft, true)]
    [InlineData(InventoryReceiptStatus.Received, false)]
    [InlineData(InventoryReceiptStatus.Cancelled, false)]
    public void ValidateReceipt(InventoryReceiptStatus status, bool allowed)
    {
        // Act
        var receive = InventoryMovementLogic.ValidateReceiptReceive(status);
        var cancel = InventoryMovementLogic.ValidateReceiptCancel(status);

        // Assert
        Assert.Equal(allowed, receive is null);
        Assert.Equal(allowed, cancel is null);
        if (!allowed)
        {
            Assert.Equal((ErrorCode.InventoryReceiptStatusInvalid, RuleReason.InventoryReceiptNotDraft), (receive!.Code, receive.Reason));
        }
    }

    // 出荷とキャンセルは依頼のとき、受領は出荷済みのときだけ
    [Theory]
    [InlineData(InventoryTransferStatus.Requested, true, false)]
    [InlineData(InventoryTransferStatus.Shipped, false, true)]
    [InlineData(InventoryTransferStatus.Received, false, false)]
    [InlineData(InventoryTransferStatus.Cancelled, false, false)]
    public void ValidateTransfer(InventoryTransferStatus status, bool shippable, bool receivable)
    {
        // Act
        var ship = InventoryMovementLogic.ValidateTransferShip(status);
        var cancel = InventoryMovementLogic.ValidateTransferCancel(status);
        var receive = InventoryMovementLogic.ValidateTransferReceive(status);

        // Assert
        Assert.Equal(shippable, ship is null);
        Assert.Equal(shippable, cancel is null);
        Assert.Equal(receivable, receive is null);
        if (!receivable)
        {
            Assert.Equal((ErrorCode.InventoryTransferStatusInvalid, RuleReason.InventoryTransferNotShipped), (receive!.Code, receive.Reason));
        }
    }
}
