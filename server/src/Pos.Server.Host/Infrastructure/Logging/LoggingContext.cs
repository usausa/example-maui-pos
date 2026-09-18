namespace Pos.Server.Host.Infrastructure.Logging;

public static class LoggingContext
{
    private static readonly AsyncLocal<string?> Local = new();

    public static string? RemoteIpAddress => Local.Value;

    public static void Set(string? remoteIpAddress) =>
        Local.Value = remoteIpAddress;

    public static void Clear() =>
        Local.Value = null;
}
