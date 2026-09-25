namespace Pos.Server.Models.Entity;

[Name("Accounts")]
public sealed class AccountEntity
{
    [Key]
    public Guid Id { get; set; }

    // ログイン ID
    public string Name { get; set; } = default!;

    // ソルト + ハッシュ (IPasswordProvider)
#pragma warning disable CA1819
    public byte[] Password { get; set; } = default!;
#pragma warning restore CA1819

    public AccountRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // 役割・有効・パスワードを変えると増え、ログイン中のセッションを無効にする
    public int Version { get; set; }
}
