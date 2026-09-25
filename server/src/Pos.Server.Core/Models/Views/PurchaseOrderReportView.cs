namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 発注書 PDF の入力 (発注一式 + 発注元の会社と店舗 + 仕入先 + 店舗のタイムゾーン)
public sealed class PurchaseOrderReportView
{
    public required PurchaseOrderDetailView Detail { get; init; }

    public required string CompanyName { get; init; }

    public StoreEntity? Store { get; init; }

    public SupplierEntity? Supplier { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }
}
