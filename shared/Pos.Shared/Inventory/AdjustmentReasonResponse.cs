namespace Pos.Shared.Inventory;

using Pos.Shared.Common;

// 在庫調整理由 (破損 / 廃棄 / 万引き / 自家消費 / 棚卸差異 ...)
public sealed class AdjustmentReasonResponse
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class AdjustmentReasonListResponse : ListResponse<AdjustmentReasonResponse>
{
}
