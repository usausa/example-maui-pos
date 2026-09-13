namespace Pos.Server.Models.Entity;

public sealed class TransactionLineEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    public decimal ListPrice { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal TaxRate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal PointRate { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AllocatedDiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public int PointsRedeemed { get; set; }

    public int PointsEarned { get; set; }

    public Guid? OriginalLineId { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public string? Note { get; set; }
}
