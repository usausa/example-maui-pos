namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class AdjustmentReasonForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial AdjustmentReasonForm ToForm(AdjustmentReasonEntity entity);

    [Mapper]
    public static partial AdjustmentReasonEntity ToEntity(AdjustmentReasonForm form);
}
