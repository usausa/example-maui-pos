namespace Pos.Server.Models.Entity;

// 締めた時点の日計 (締めを解除すると内訳ごと消す)
[Name("DailyClosings")]
public sealed class DailyClosingEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public DateTime ClosedAt { get; set; }

    public string? ClosedBy { get; set; }

    public int ShiftCount { get; set; }

    public int SalesCount { get; set; }

    public int ReturnCount { get; set; }

    public int VoidCount { get; set; }

    public int CustomerCount { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }

    public decimal NetSales { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    public bool HasLateTransactions { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
