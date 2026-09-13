namespace Pos.Server.Models.Entity;

public sealed class TransactionTaxSummaryEntity
{
    [Key]
    public Guid TransactionId { get; set; }

    [Key]
    public Guid TaxRateId { get; set; }

    [Key]
    public bool TaxIncluded { get; set; }

    public decimal Rate { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}
