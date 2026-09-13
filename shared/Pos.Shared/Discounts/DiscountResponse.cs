namespace Pos.Shared.Discounts;

using Pos.Shared.Common;

public sealed class DiscountResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    // 金額 (円) または率 (0.10)
    public decimal Value { get; set; }

    public DiscountScope Scope { get; set; }

    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class DiscountListResponse : ListResponse<DiscountResponse>;
