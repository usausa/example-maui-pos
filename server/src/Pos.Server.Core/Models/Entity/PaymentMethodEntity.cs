namespace Pos.Server.Models.Entity;

[Name("PaymentMethods")]
public sealed class PaymentMethodEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? ShortName { get; set; }

    public PaymentKind Kind { get; set; }

    public bool AllowsChange { get; set; }

    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
