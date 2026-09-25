namespace Pos.Domain.Enums;

// Deposit は受注の前受金を会計で充てる支払 (前受金は受注で受け取る)
public enum PaymentKind
{
    Cash,
    Card,
    Qr,
    EMoney,
    Voucher,
    Points,
    Credit,
    Other,
    Deposit
}

public static class PaymentKindExtensions
{
    // 前受金を受け取れる支払方法 (ポイント・掛け・商品券・前受金は除く)
    public static bool CanReceiveDeposit(this PaymentKind kind) => kind is PaymentKind.Cash or PaymentKind.Card or PaymentKind.Qr or PaymentKind.EMoney;
}
