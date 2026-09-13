namespace Pos.Shared.Categories;

using Pos.Shared.Common;

public sealed class CategoryResponse
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

public sealed class CategoryListResponse : ListResponse<CategoryResponse>
{
}
