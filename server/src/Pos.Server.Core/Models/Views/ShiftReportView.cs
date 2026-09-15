namespace Pos.Server.Models.Views;

// 精算レポート PDF の入力 (集計 + 表示名 + 店舗のタイムゾーン)
public sealed class ShiftReportView
{
    public required ShiftSummaryView Summary { get; init; }

    public required string StoreName { get; init; }

    public required string TerminalName { get; init; }

    public required int TerminalNo { get; init; }

    public required string OpenedBy { get; init; }

    public string? ClosedBy { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }
}
