namespace Pos.Contract.Categories;

using Pos.Contract;

public sealed class CategoryResponseItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    // null = 最上位
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class CategoryResponse : ListResponse<CategoryResponseItem>;
