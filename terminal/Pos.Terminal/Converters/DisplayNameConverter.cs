namespace Pos.Terminal.Converters;

using Pos.Terminal.Models.Entity;

// 列挙型 → 表示文言 (文言は DisplayText に集約する)
public sealed class DisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            null => null,
            TransactionType x => DisplayText.Name(x),
            TransactionStatus x => DisplayText.Name(x),
            ShiftStatus x => DisplayText.Name(x),
            CashEventType x => DisplayText.Name(x),
            PointHistoryType x => DisplayText.Name(x),
            PaymentKind x => DisplayText.Name(x),
            StaffRole x => DisplayText.Name(x),
            OutboxStatus x => DisplayText.Name(x),
            OutboxKind x => DisplayText.Name(x),
            _ => value.ToString()
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
