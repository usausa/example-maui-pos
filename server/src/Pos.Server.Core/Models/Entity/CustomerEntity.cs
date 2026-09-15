namespace Pos.Server.Models.Entity;

[Name("Customers")]
public sealed class CustomerEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Kana { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public DateOnly? BirthDate { get; set; }

    // PointHistories の集計を非正規化した現在残高
    public int PointBalance { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
