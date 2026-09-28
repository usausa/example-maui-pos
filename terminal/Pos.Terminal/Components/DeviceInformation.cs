namespace Pos.Terminal.Components;

using System.Diagnostics;

public sealed record NetworkStatus(
    NetworkAccess Access,
    IReadOnlyList<ConnectionProfile> Profiles);

[System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Global", Justification = "Process statistics are provided for diagnostic consumers.")]
public readonly record struct ProcessStatistics(
    long Timestamp,
    TimeSpan CpuTime,
    long WorkingSet,
    int ThreadCount,
    int Gc0Count,
    int Gc1Count,
    int Gc2Count,
    long AllocatedBytes);

public sealed partial class DeviceInformation : IDisposable
{
    private bool started;

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        StartNetwork();
    }

    public void Stop()
    {
        if (!started)
        {
            return;
        }

        started = false;
        StopNetwork();
    }

    public string DeviceId { get; } = ResolveDeviceId();

    private static partial string ResolveDeviceId();

    public event EventHandler? NetworkChanged;

    public NetworkStatus? Network { get; private set; }

    private void UpdateNetwork(NetworkStatus status)
    {
        Network = status;
        NetworkChanged?.Invoke(this, EventArgs.Empty);
    }

    private partial void StartNetwork();

    private partial void StopNetwork();

    public DateTime StartTime { get; } = ReadStartTime();

    public ProcessStatistics ReadProcessStatistics()
    {
        ReadProcessStat(out var threadCount, out var workingSet);
        return new ProcessStatistics(
            Stopwatch.GetTimestamp(),
            Environment.CpuUsage.TotalTime,
            workingSet,
            threadCount,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GC.GetTotalAllocatedBytes());
    }

    public static long ReadHeapSize() => GC.GetGCMemoryInfo().HeapSizeBytes;

    private static DateTime ReadStartTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.StartTime;
    }

    private partial void ReadProcessStat(out int threadCount, out long workingSet);
}
