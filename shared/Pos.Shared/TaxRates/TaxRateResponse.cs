namespace Pos.Shared.TaxRates;

using Pos.Shared.Common;

public sealed class TaxRateResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    // 0.10 = 10%
    public decimal Rate { get; set; }

    public TaxKind Kind { get; set; }

    // 商品登録時の既定
    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class TaxRateListResponse : ListResponse<TaxRateResponse>
{
}
