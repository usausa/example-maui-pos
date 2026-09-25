namespace Pos.Server.Host.Models.Forms;

// ユーザーのパスワード変更
public sealed class AccountPasswordForm
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string PasswordConfirm { get; set; } = string.Empty;

    public int Version { get; set; }
}
