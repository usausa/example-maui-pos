namespace Pos.Terminal.Converters;

using Pos.Terminal.Models.Entity;

// 列挙型 → 表示文言 (文言は ViewHelper に集約する)
public sealed class DisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            null => null,
            TransactionType x => ViewHelper.Name(x),
            TransactionStatus x => ViewHelper.Name(x),
            ShiftStatus x => ViewHelper.Name(x),
            CashEventType x => ViewHelper.Name(x),
            OrderDepositType x => ViewHelper.Name(x),
            PointHistoryType x => ViewHelper.Name(x),
            PaymentKind x => ViewHelper.Name(x),
            StaffRole x => ViewHelper.Name(x),
            OutboxStatus x => ViewHelper.Name(x),
            OutboxKind x => ViewHelper.Name(x),
            OrderStatus x => ViewHelper.Name(x),
            OrderType x => ViewHelper.Name(x),
            ReceivingKind x => ViewHelper.Name(x),
            ReceivingLineState x => ViewHelper.Name(x),
            _ => value.ToString()
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
