namespace Pos.Server.Models.Entity;

public sealed class StaffEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public StaffRole Role { get; set; }

    public Guid? StoreId { get; set; }

    // Phase 2 (認証)。MVP では未使用
#pragma warning disable CA1819
    public byte[]? PinHash { get; set; }
#pragma warning restore CA1819

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
