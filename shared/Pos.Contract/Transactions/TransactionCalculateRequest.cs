namespace Pos.Contract.Transactions;

// 入力項目だけを送り、計算項目を受け取る (POST /transactions/calculate)。明細などに含まれる計算項目は無視される
public sealed class TransactionCalculateRequest : IValidatableObject
{
    public TransactionType Type { get; set; }

    // Return のとき必須
    public Guid? OriginalTransactionId { get; set; }

    [Required]
    public IReadOnlyList<TransactionCreateRequestLine> Lines { get; set; } = default!;

    [Required]
    public IReadOnlyList<TransactionCreateRequestDiscount> Discounts { get; set; } = [];

    [Required]
    public IReadOnlyList<TransactionCreateRequestPayment> Payments { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 項目の検証は一覧の null の要素を飛ばすので、要素を読む前に拒む
        if (Lines.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("明細に null は指定できません", [nameof(Lines)]);
            yield break;
        }

        if (Lines.Any(static x => x.SerialNumbers.Cast<object?>().Contains(null)))
        {
            yield return new ValidationResult("シリアル番号に null は指定できません", [nameof(Lines)]);
            yield break;
        }

        if (Discounts.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("値引に null は指定できません", [nameof(Discounts)]);
            yield break;
        }

        if (Payments.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("支払に null は指定できません", [nameof(Payments)]);
        }
    }
}
