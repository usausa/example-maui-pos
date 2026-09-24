namespace Pos.Server.Models.Entity;

// 締めた時点の支払方法別 (名称も写す)
[Name("DailyClosingPayments")]
public sealed class DailyClosingPaymentEntity
{
    [Key]
    public Guid DailyClosingId { get; set; }

    [Key]
    public int LineNo { get; set; }

    public Guid PaymentMethodId { get; set; }

    public string Name { get; set; } = default!;

    public PaymentKind Kind { get; set; }

    public decimal SalesAmount { get; set; }

    public int SalesCount { get; set; }

    public decimal ReturnAmount { get; set; }

    public int ReturnCount { get; set; }
}
