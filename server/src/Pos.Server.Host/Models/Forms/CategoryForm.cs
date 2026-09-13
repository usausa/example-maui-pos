namespace Pos.Server.Host.Models.Forms;

public sealed class CategoryForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // null = 大分類
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
