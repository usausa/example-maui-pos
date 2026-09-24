namespace Pos.Server.Models.Entity;

// 締めた時点の税率別
[Name("DailyClosingTaxes")]
public sealed class DailyClosingTaxEntity
{
    [Key]
    public Guid DailyClosingId { get; set; }

    [Key]
    public int LineNo { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}
