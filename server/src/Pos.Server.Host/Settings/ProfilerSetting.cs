namespace Pos.Server.Host.Settings;

public sealed class ProfilerSetting
{
    [Required]
    [ValidateObjectMembers]
    public SqlLogSetting SqlLog { get; set; } = default!;

    [Required]
    [ValidateObjectMembers]
    public SqlTelemetrySetting SqlTelemetry { get; set; } = default!;
}
