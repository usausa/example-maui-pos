namespace Pos.Server.Models.Views;

// 各項目は Host の Mapper (ソース生成) が読む
// ReSharper disable NotAccessedPositionalProperty.Global
// シフトの支払方法別集計
public sealed record PaymentMethodTotalView(
    Guid PaymentMethodId,
    string Name,
    PaymentKind Kind,
    decimal SalesAmount,
    int SalesCount,
    decimal ReturnAmount,
    int ReturnCount);
