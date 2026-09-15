namespace Pos.Server.Host.Reports;

using OysterReport;

// 帳票の表示文字列 (数値は 3 桁区切り、日時は店舗のタイムゾーン)
internal static class ReportText
{
    public static string Yen(decimal value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string Yen(decimal? value) => value is null ? String.Empty : Yen(value.Value);

    public static string Count(int value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string Quantity(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string Percent(decimal rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Date(DateOnly value) => value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime? utc, TimeZoneInfo timeZone) => utc is null ? String.Empty : DateTime(utc.Value, timeZone);

    public static string Time(DateTime utc, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Time(DateTime? utc, TimeZoneInfo timeZone) => utc is null ? String.Empty : Time(utc.Value, timeZone);

    // 明細行: プレースホルダの行を件数分に増やしてから順に埋める。0 件は行ごと消す
    public static void FillRows(TemplateSheet sheet, string marker, IReadOnlyList<Dictionary<string, string?>> rows)
    {
        var row = sheet.FindRow(marker);
        if (rows.Count == 0)
        {
            row.Delete();
            return;
        }

        for (var i = 1; i < rows.Count; i++)
        {
            row = row.InsertCopyBelow();
        }

        sheet.ReplacePlaceholders(rows);
    }
}
