namespace Pos.Contract.Shifts;

// 精算 (POST /shifts/{id}/close)
public sealed class ShiftCloseRequest
{
    public DateTime ClosedAt { get; set; }

    public Guid ClosedByStaffId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ActualCash { get; set; }

    // 金種別枚数 (任意)
    public IReadOnlyList<ShiftCloseRequestDenomination> Denominations { get; set; } = [];

    [MaxLength(Length.Note)]
    public string? Note { get; set; }
}

public sealed class ShiftCloseRequestDenomination
{
    // 10000, 5000, 1000, 500, 100, 50, 10, 5, 1
    public int Denomination { get; set; }

    [Range(0, int.MaxValue)]
    public int Count { get; set; }
}
