namespace Pos.Server.Models.Parameters;

using Pos.Server.Models.Entity;

// 精算の入力
public sealed class ShiftCloseParameter
{
    public DateTime ClosedAt { get; set; }

    public Guid ClosedByStaffId { get; set; }

    public decimal ActualCash { get; set; }

    public string? Note { get; set; }

    // 金種別枚数 (任意)
    public IReadOnlyList<ShiftDenominationEntity> Denominations { get; set; } = [];
}
