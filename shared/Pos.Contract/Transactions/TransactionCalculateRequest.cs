namespace Pos.Contract.Transactions;

// 入力項目だけを送り、計算項目を受け取る (POST /transactions/calculate)。明細などに含まれる計算項目は無視される
public sealed class TransactionCalculateRequest
{
    public TransactionType Type { get; set; }

    // Return のとき必須
    public Guid? OriginalTransactionId { get; set; }

    [Required]
    public IReadOnlyList<TransactionRequestLine> Lines { get; set; } = default!;

    public IReadOnlyList<TransactionRequestDiscount> Discounts { get; set; } = [];

    public IReadOnlyList<TransactionRequestPayment> Payments { get; set; } = [];
}
