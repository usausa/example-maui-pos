namespace Pos.Server.Host.Models.Forms;

// スタッフの PIN の設定 (端末のログインと承認に使う)
public sealed class StaffPinForm
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Pin { get; set; } = string.Empty;

    public string PinConfirm { get; set; } = string.Empty;

    public int Version { get; set; }
}
