namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 受注票 PDF の入力 (受注一式 + 会社と店舗 + 担当と支払方法の名前 + 店舗のタイムゾーン)
public sealed class OrderReportView
{
    public required OrderDetailView Detail { get; init; }

    public required string CompanyName { get; init; }

    public StoreEntity? Store { get; init; }

    public required string StaffName { get; init; }

    public required IReadOnlyDictionary<Guid, string> PaymentMethodNames { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }
}
