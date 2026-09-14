namespace Pos.Terminal.Converters;

// 空なら ConverterParameter (プレースホルダ) を表示する
public sealed class EmptyTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? text : parameter;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
