namespace Pos.Terminal;

internal static partial class Log
{
    // Startup

    [LoggerMessage(Level = LogLevel.Information, Message = "Application start. version=[{version}], runtime=[{runtime}]")]
    public static partial void InfoApplicationStart(this ILogger logger, Version? version, Version runtime);

    // State

    [LoggerMessage(Level = LogLevel.Debug, Message = "Screen state changed. state=[{on}]")]
    public static partial void DebugScreenStateChanged(this ILogger logger, bool on);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Battery info changed. level=[{chargeLevel}], state=[{state}], source=[{powerSource}]")]
    public static partial void DebugBatteryState(this ILogger logger, double chargeLevel, BatteryState state, BatteryPowerSource powerSource);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Connectivity changed. profile=[{profile}], access=[{access}]")]
    public static partial void DebugConnectivityState(this ILogger logger, NetworkProfile profile, NetworkAccess access);

    // Network

    [LoggerMessage(Level = LogLevel.Warning, Message = "API call failed. status=[{status}], statusCode=[{statusCode}], errorCode=[{errorCode}]")]
    public static partial void WarnApiFailed(this ILogger logger, Services.ApiStatus status, int statusCode, string? errorCode, Exception? exception);

    // Sync

    [LoggerMessage(Level = LogLevel.Information, Message = "Master synchronized. products=[{products}], serverTime=[{serverTime}]")]
    public static partial void InfoMasterSynced(this ILogger logger, int products, DateTime serverTime);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox rejected. kind=[{kind}], target=[{targetId}], statusCode=[{statusCode}], errorCode=[{errorCode}]")]
    public static partial void WarnOutboxRejected(this ILogger logger, Models.Entity.OutboxKind kind, Guid targetId, int statusCode, string? errorCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync loop failed.")]
    public static partial void ErrorSyncLoop(this ILogger logger, Exception exception);
}
