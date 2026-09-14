namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// シフトと集計・金種。ExpectedCash は Open 中は集計から求め、Closed は確定値
public sealed class ShiftDetail
{
    public required ShiftEntity Shift { get; init; }

    public required ShiftTotals Totals { get; init; }

    public required IReadOnlyList<ShiftDenominationEntity> Denominations { get; init; }

    public required decimal? ExpectedCash { get; init; }
}
