namespace Pos.Contract.Categories;

public sealed class CategoryCreateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.Name)]
    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }
}
