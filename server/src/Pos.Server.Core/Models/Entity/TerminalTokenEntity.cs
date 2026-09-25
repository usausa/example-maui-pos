namespace Pos.Server.Models.Entity;

// 端末の登録。ペアリングコードを発行した行が、ペアリングでトークンを持つ行になる (コードは消費する)
[Name("TerminalTokens")]
public sealed class TerminalTokenEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TerminalId { get; set; }

    public string? PairingCode { get; set; }

    public DateTime? PairingExpiresAt { get; set; }

    // トークンの SHA-256 (トークン自体は保存しない)
#pragma warning disable CA1819
    public byte[]? TokenHash { get; set; }
#pragma warning restore CA1819

    public string? DeviceName { get; set; }

    public DateTime? PairedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
