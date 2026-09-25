namespace Pos.Server.Models.Views;

// 端末ごとの登録の状態 (ペアリング済みの最新の行。解除済みなら RevokedAt が入る)
public sealed class TerminalRegistrationView
{
    public Guid TerminalId { get; set; }

    public string? DeviceName { get; set; }

    public DateTime? PairedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
