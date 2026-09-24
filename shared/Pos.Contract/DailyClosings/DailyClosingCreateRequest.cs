namespace Pos.Contract.DailyClosings;

// 締め (POST /daily-closings)。店舗 × 営業日のシフトがすべて精算済みであること
public sealed class DailyClosingCreateRequest
{
    public Guid StoreId { get; set; }

    public DateOnly BusinessDate { get; set; }
}
