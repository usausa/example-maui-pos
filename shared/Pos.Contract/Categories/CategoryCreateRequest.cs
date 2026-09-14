namespace Pos.Contract.Categories;

public sealed class CategoryCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }
}
