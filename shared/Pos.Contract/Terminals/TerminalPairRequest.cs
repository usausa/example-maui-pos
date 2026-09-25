namespace Pos.Contract.Terminals;

// 管理画面で発行したペアリングコードで端末を登録する
public sealed class TerminalPairRequest
{
    [Required]
    [MaxLength(Length.PairingCodeDigits)]
    public string PairingCode { get; set; } = default!;

    [Required]
    [MaxLength(Length.DeviceName)]
    public string DeviceName { get; set; } = default!;

    [MaxLength(Length.AppVersion)]
    public string? AppVersion { get; set; }
}
