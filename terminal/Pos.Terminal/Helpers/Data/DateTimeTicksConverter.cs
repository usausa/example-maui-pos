namespace Pos.Terminal.Helpers.Data;

using Smart.Data.Accessor.Converters;

// 日時は INTEGER (UTC ticks) で保存する
public sealed class DateTimeTicksConverter : IValueConverter<long, DateTime>
{
    public static DateTime FromDb(long dbValue) => new(dbValue, DateTimeKind.Utc);

    public static long ToDb(DateTime clrValue) => (clrValue.Kind == DateTimeKind.Local ? clrValue.ToUniversalTime() : clrValue).Ticks;
}
