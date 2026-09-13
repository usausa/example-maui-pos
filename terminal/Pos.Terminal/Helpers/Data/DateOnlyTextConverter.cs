namespace Pos.Terminal.Helpers.Data;

using Smart.Data.Accessor.Converters;

// 営業日などの日付は yyyy-MM-dd の TEXT
public sealed class DateOnlyTextConverter : IValueConverter<string, DateOnly>
{
    public static DateOnly FromDb(string dbValue) => DateOnly.ParseExact(dbValue, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ToDb(DateOnly clrValue) => clrValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
