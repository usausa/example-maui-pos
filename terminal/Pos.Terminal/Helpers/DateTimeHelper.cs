namespace Pos.Terminal.Helpers;

// 日付・時刻の書式をここに集約する
public static class DateTimeHelper
{
    private const string IsoDateFormat = "yyyy-MM-dd";

    private const string IsoDateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    private const string CompactDateFormat = "yyyyMMdd";

    // 表示用の書式 (XAML の DatePicker からも使う)
    public const string DateFormat = "yyyy/MM/dd";

    private const string DateTimeFormat = "yyyy/MM/dd HH:mm";

    private const string TimestampFormat = "yyyy/MM/dd HH:mm:ss";

    private const string TimeFormat = "HH:mm";

    private static readonly string[] DateFormats = [DateFormat, IsoDateFormat, CompactDateFormat];

    // 交換用 (API・DB)

    public static string ToIsoDate(DateOnly value) => value.ToString(IsoDateFormat, CultureInfo.InvariantCulture);

    public static DateOnly ParseIsoDate(string value) => DateOnly.ParseExact(value, IsoDateFormat, CultureInfo.InvariantCulture);

    public static string ToIsoDateTime(DateTime value) => value.ToUniversalTime().ToString(IsoDateTimeFormat, CultureInfo.InvariantCulture);

    public static string ToRoundTrip(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static bool TryParseRoundTrip(string? value, out DateTime result) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result);

    // 表示用

    public static string FormatDate(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime value) => value.ToLocalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    public static string FormatTimestamp(DateTime value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static string FormatTime(DateTime value) => value.ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);

    // 入力用 (yyyy/MM/dd・yyyy-MM-dd・yyyyMMdd)

    public static string ToCompactDate(DateOnly value) => value.ToString(CompactDateFormat, CultureInfo.InvariantCulture);

    public static bool TryParseCompactDate(string value, out DateOnly result) =>
        DateOnly.TryParseExact(value, CompactDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    public static bool TryParseDate(string value, out DateOnly result) =>
        DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
