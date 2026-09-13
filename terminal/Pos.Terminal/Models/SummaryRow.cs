namespace Pos.Terminal.Models;

// 集計・詳細画面の 1 行 (見出しと値)
public sealed record SummaryRow(string Label, string Value);

public sealed record SummarySection(string Title, IReadOnlyList<SummaryRow> Rows);
