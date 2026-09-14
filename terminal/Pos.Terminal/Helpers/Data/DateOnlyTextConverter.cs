namespace Pos.Terminal.Helpers.Data;

using Smart.Data.Accessor.Converters;

// 営業日などの日付は yyyy-MM-dd の TEXT
public sealed class DateOnlyTextConverter : IValueConverter<string, DateOnly>
{
    public static DateOnly FromDb(string dbValue) => DateTimeHelper.ParseIsoDate(dbValue);

    public static string ToDb(DateOnly clrValue) => DateTimeHelper.ToIsoDate(clrValue);
}
