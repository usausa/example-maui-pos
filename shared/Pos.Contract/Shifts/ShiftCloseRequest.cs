namespace Pos.Contract.Shifts;

// 精算 (POST /shifts/{id}/close)
public sealed class ShiftCloseRequest : IValidatableObject
{
    public DateTime ClosedAt { get; set; }

    public Guid ClosedByStaffId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualCash { get; set; }

    // 金種別枚数 (任意)
    [Required]
    public IReadOnlyList<ShiftCloseRequestDenomination> Denominations { get; set; } = [];

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 項目の検証は一覧の null の要素を飛ばすので、要素を読む前に拒む
        if (Denominations.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("金種に null は指定できません", [nameof(Denominations)]);
            yield break;
        }

        // 金種はシフトの中で一意 (表の主キー) なので、重複は書く前に拒む
        if (Denominations.Select(static x => x.Denomination).Distinct().Count() != Denominations.Count)
        {
            yield return new ValidationResult("金種が重複しています", [nameof(Denominations)]);
        }
    }
}

public sealed class ShiftCloseRequestDenomination
{
    // 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1
    public int Denomination { get; set; }

    [Range(0, int.MaxValue)]
    public int Count { get; set; }
}
