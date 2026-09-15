namespace Pos.Terminal.Converters;

// 金額 → ¥1,234 (負数は -¥1,234)
public sealed class YenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            decimal d => ViewHelper.Yen(d),
            int i => ViewHelper.Yen(i),
            long l => ViewHelper.Yen(l),
            _ => null
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
