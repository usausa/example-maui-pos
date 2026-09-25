namespace Pos.Server.Models.Entity;

[Name("Staff")]
public sealed class StaffEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public StaffRole Role { get; set; }

    public Guid? StoreId { get; set; }

    // PIN のハッシュ (PinHasher)。端末向けの同期応答にだけ含め、端末がオフラインでも照合する
#pragma warning disable CA1819
    public byte[]? PinHash { get; set; }
#pragma warning restore CA1819

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
