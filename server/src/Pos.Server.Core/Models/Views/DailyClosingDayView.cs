namespace Pos.Server.Models.Views;

// 各項目は Host の Mapper (ソース生成) が読む
// ReSharper disable NotAccessedPositionalProperty.Global
// 店舗 × 営業日の 1 行。締め済みは締めた時点の日計、未締めは取引からの集計 (取消済みを除き、返品は負)。
// シフト数は締めた時点、未精算のシフト数は現在の状態
public sealed record DailyClosingDayView(
    Guid StoreId,
    DateOnly BusinessDate,
    DailyClosingStatus Status,
    Guid? Id,
    int ShiftCount,
    int OpenShiftCount,
    int SalesCount,
    int ReturnCount,
    int VoidCount,
    int CustomerCount,
    decimal SalesTotal,
    decimal ReturnsTotal,
    decimal NetSales,
    decimal DiscountTotal,
    decimal TaxTotal,
    int PointsEarned,
    int PointsRedeemed,
    bool HasLateTransactions,
    DateTime? ClosedAt,
    string? ClosedBy)
{
    // シフトも取引もない日
    public static DailyClosingDayView Empty(Guid storeId, DateOnly businessDate) =>
        new(storeId, businessDate, DailyClosingStatus.Open, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, null, null);
}
