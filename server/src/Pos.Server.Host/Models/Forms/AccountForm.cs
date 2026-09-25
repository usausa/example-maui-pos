namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

// ユーザー (管理画面のアカウント) の追加・編集。パスワードは追加のときだけ入れる (変更は AccountPasswordForm)
public sealed class AccountForm
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string PasswordConfirm { get; set; } = string.Empty;

    public AccountRole Role { get; set; } = AccountRole.Operator;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    public bool IsNew => Id == Guid.Empty;

    public static AccountForm ToForm(AccountEntity entity) =>
        new() { Id = entity.Id, Name = entity.Name, Role = entity.Role, IsActive = entity.IsActive, Version = entity.Version };
}
