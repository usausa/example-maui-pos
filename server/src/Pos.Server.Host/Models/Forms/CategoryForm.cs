namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class CategoryForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // null = 大分類
    public Guid? ParentId { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial CategoryForm ToForm(CategoryEntity entity);

    [Mapper]
    public static partial CategoryEntity ToEntity(CategoryForm form);
}
