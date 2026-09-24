namespace Pos.Contract.Suppliers;

using Pos.Contract;

// 仕入先 (入荷の相手)
public sealed class SupplierResponseItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class SupplierResponse : ListResponse<SupplierResponseItem>;
