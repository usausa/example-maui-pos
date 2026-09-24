namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// レシート PDF (控え・再発行) の入力 (取引一式 + 店舗 + 表示名 + 店舗のタイムゾーン)
public sealed class ReceiptReportView
{
    public required TransactionDetailView Detail { get; init; }

    public StoreEntity? Store { get; init; }

    public required string TerminalName { get; init; }

    public required string StaffName { get; init; }

    public required IReadOnlyDictionary<Guid, string> PaymentMethodNames { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }
}
