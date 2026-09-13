namespace Pos.Server.Infrastructure.Data;

using Smart.Data.Accessor.Converters;

// 日付を yyyy-MM-dd の TEXT で保存する (db-design §1)
public sealed class DateOnlyTextConverter : IValueConverter<string, DateOnly>
{
    public static DateOnly FromDb(string dbValue) => DateOnly.ParseExact(dbValue, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ToDb(DateOnly clrValue) => clrValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
