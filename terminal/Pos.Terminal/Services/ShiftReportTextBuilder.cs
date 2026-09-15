namespace Pos.Terminal.Services;

// 精算レポートの共有用テキスト
public static class ShiftReportTextBuilder
{
    public static string Build(string header, string source, IEnumerable<SummarySection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine("精算レポート").AppendLine(header).AppendLine(source);
        foreach (var section in sections)
        {
            sb.AppendLine().AppendLine(section.Title);
            foreach (var row in section.Rows)
            {
                sb.Append(row.Label).Append(": ").AppendLine(row.Value);
            }
        }

        return sb.ToString();
    }
}
