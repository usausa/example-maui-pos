namespace Pos.Contract.Terminals;

public sealed class TerminalHeartbeatRequest
{
    [MaxLength(Length.AppVersion)]
    public string? AppVersion { get; set; }
}
