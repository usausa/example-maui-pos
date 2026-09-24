namespace Pos.Contract.DailyClosings;

using Pos.Contract;

// 店舗 × 営業日。締め済みは締めた時点の日計、未締めは取引からの集計 (取消済みを除き、返品は負)
public sealed class DailyClosingResponseItem
{
    // 締め済みのときだけ
    public Guid? Id { get; set; }

    public Guid StoreId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public DailyClosingStatus Status { get; set; }

    // 締め後に同じ営業日の取引が届いた (締め直すまで日計に含まれない)
    public bool HasLateTransactions { get; set; }

    // その営業日のシフトと、その営業日の取引を含むシフト
    public int ShiftCount { get; set; }

    // 未精算のシフト (現在の状態。1 件でもあれば締められない)
    public int OpenShiftCount { get; set; }

    public int SalesCount { get; set; }

    public int ReturnCount { get; set; }

    public int VoidCount { get; set; }

    public int CustomerCount { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }

    public decimal NetSales { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    public DateTime? ClosedAt { get; set; }

    // 締めた管理画面のアカウント名
    public string? ClosedBy { get; set; }
}

public sealed class DailyClosingResponse : ListResponse<DailyClosingResponseItem>;
