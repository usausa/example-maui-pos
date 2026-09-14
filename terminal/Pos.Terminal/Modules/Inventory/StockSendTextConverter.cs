namespace Pos.Terminal.Modules.Inventory;

// 入力件数 → 送信ボタンの文言 (件数があれば付ける)
public sealed class StockSendTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && (count > 0) ? $"送信 ({count})" : "送信";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
