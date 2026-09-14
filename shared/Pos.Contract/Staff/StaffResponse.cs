namespace Pos.Contract.Staff;

using Pos.Contract;

public sealed class StaffResponseItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public StaffRole Role { get; set; }

    // null = 本部 (全店)
    public Guid? StoreId { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class StaffResponse : ListResponse<StaffResponseItem>;
