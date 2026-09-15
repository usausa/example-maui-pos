namespace Pos.Contract.Transactions;

// 入力項目だけを送り、計算項目を受け取る (POST /transactions/calculate)。明細などに含まれる計算項目は無視される
public sealed class TransactionCalculateRequest
{
    public TransactionType Type { get; set; }

    // Return のとき必須
    public Guid? OriginalTransactionId { get; set; }

    [Required]
    public IReadOnlyList<TransactionCreateRequestLine> Lines { get; set; } = default!;

    public IReadOnlyList<TransactionCreateRequestDiscount> Discounts { get; set; } = [];

    public IReadOnlyList<TransactionCreateRequestPayment> Payments { get; set; } = [];
}
