namespace Pos.Contract.Orders;

// 受注のキャンセル (POST /orders/{id}/cancel)。未完了のときだけ
public sealed class OrderCancelRequest
{
    [MaxLength(Length.Reason)]
    public string? Reason { get; set; }
}
