namespace Pos.Terminal.Helpers;

using System.Text.Json;
using System.Text.Json.Serialization;

// 異常終了の記録 (次の起動で知らせる)
public sealed record CrashInfo
{
    public required DateTime Time { get; init; }

    public required string Version { get; init; }

    public required string Device { get; init; }

    public required string ExceptionType { get; init; }

    public required string Detail { get; init; }

    public bool Shown { get; init; }

    public string ToReport()
    {
        var report = new StringBuilder();
        report.AppendLine($"日時: {DateTimeHelper.FormatTimestamp(Time)}");
        report.AppendLine($"バージョン: {Version}");
        report.AppendLine($"機種: {Device}");
        report.AppendLine("例外:");
        report.AppendLine(Detail);
        return report.ToString();
    }
}

[JsonSerializable(typeof(CrashInfo))]
internal sealed partial class CrashInfoJsonContext : JsonSerializerContext;

public static partial class CrashReport
{
    private static Exception? lastException;

    public static void Start()
    {
        AppDomain.CurrentDomain.UnhandledException += static (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogException(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += static (_, args) => LogException(args.Exception);

        PlatformStart();
    }

    private static partial void PlatformStart();

    private static partial string ResolveCrashPath();

    public static void LogException(Exception e)
    {
        // 同じ例外が複数の口から届くので 1 回だけ残す
        if (ReferenceEquals(Interlocked.Exchange(ref lastException, e), e))
        {
            return;
        }

#pragma warning disable CA1031
        try
        {
            var device = DeviceInfo.Current;
            Save(new CrashInfo
            {
                Time = DateTime.Now,
                Version = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})",
                Device = $"{device.Manufacturer} {device.Model} / {device.Platform} {device.VersionString}",
                ExceptionType = e.GetType().FullName ?? e.GetType().Name,
                Detail = e.ToString()
            });
        }
        catch
        {
            // Ignore
        }
#pragma warning restore CA1031
    }

    // 未表示の記録を知らせて表示済みにする (起動時。画面の仕組みが揃う前なので OS のダイアログで出す)
    public static async ValueTask ShowReport()
    {
        if (Load() is not { Shown: false } info)
        {
            return;
        }

        var page = Application.Current?.Windows[0].Page;
        if (page is not null)
        {
            await page.DisplayAlertAsync("前回の異常終了", info.ToReport(), "閉じる");
        }

        Save(info with { Shown = true });
    }

    private static CrashInfo? Load()
    {
        var path = ResolveCrashPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), CrashInfoJsonContext.Default.CrashInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Save(CrashInfo info) =>
        File.WriteAllText(ResolveCrashPath(), JsonSerializer.Serialize(info, CrashInfoJsonContext.Default.CrashInfo));
}
