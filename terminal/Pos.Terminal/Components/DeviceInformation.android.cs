namespace Pos.Terminal.Components;

using System.Buffers.Text;

using Android.App;
using Android.Content;
using Android.Net;

using Microsoft.Win32.SafeHandles;

using AndroidNetwork = Android.Net.Network;

public sealed partial class DeviceInformation
{
    private const int StatBufferSize = 512;

    private const int ThreadCountField = 20;
    private const int ResidentPagesField = 24;

    private static readonly long PageSize = Environment.SystemPageSize;

    private readonly NetworkCallback networkCallback;

    private readonly Dictionary<long, NetworkEntry> networks = [];

    private readonly SafeFileHandle statHandle = File.OpenHandle("/proc/self/stat");

    private ConnectivityManager? connectivityManager;

    private bool networkRegistered;

    private Transport currentTransports;

    public DeviceInformation()
    {
        networkCallback = new NetworkCallback(this);
    }

    public void Dispose()
    {
        Stop();

        networkCallback.Dispose();
        statHandle.Dispose();
    }

    private static partial string ResolveDeviceId() =>
        Android.Provider.Settings.Secure.GetString(Application.Context.ContentResolver, Android.Provider.Settings.Secure.AndroidId) ?? string.Empty;

    [Flags]
    private enum Transport
    {
        None = 0,
        Bluetooth = 0x01,
        Cellular = 0x02,
        Ethernet = 0x04,
        WiFi = 0x08
    }

    private readonly record struct NetworkEntry(Transport Transport, bool Validated);

    private partial void StartNetwork()
    {
        connectivityManager ??= (ConnectivityManager?)Application.Context.GetSystemService(Context.ConnectivityService);
        using var builder = new NetworkRequest.Builder();
        using var request = builder.Build();
        if ((connectivityManager is null) || (request is null))
        {
            return;
        }

        networks.Clear();

        connectivityManager.RegisterNetworkCallback(request, networkCallback);
        networkRegistered = true;
    }

    private partial void StopNetwork()
    {
        if (networkRegistered)
        {
            connectivityManager?.UnregisterNetworkCallback(networkCallback);
            networkRegistered = false;
        }
    }

    private void OnCapabilitiesChanged(AndroidNetwork network, NetworkCapabilities capabilities)
    {
        var transport = Transport.None;
        if (capabilities.HasTransport(TransportType.Bluetooth))
        {
            transport |= Transport.Bluetooth;
        }
        if (capabilities.HasTransport(TransportType.Cellular))
        {
            transport |= Transport.Cellular;
        }
        if (capabilities.HasTransport(TransportType.Ethernet))
        {
            transport |= Transport.Ethernet;
        }
        if (capabilities.HasTransport(TransportType.Wifi))
        {
            transport |= Transport.WiFi;
        }

        networks[network.NetworkHandle] = new NetworkEntry(transport, capabilities.HasCapability(NetCapability.Validated));
        Refresh();
    }

    private void OnLost(AndroidNetwork network)
    {
        networks.Remove(network.NetworkHandle);
        Refresh();
    }

    private void Refresh()
    {
        var access = NetworkAccess.None;
        var transports = Transport.None;
        foreach (var entry in networks.Values)
        {
            if (entry.Validated)
            {
                access = NetworkAccess.Internet;
            }
            else if (access != NetworkAccess.Internet)
            {
                access = NetworkAccess.ConstrainedInternet;
            }

            transports |= entry.Transport;
        }

        if ((Network is null) || (access != Network.Access) || (transports != currentTransports))
        {
            currentTransports = transports;
            UpdateNetwork(new NetworkStatus(access, ToProfiles(transports)));
        }
    }

    private static ConnectionProfile[] ToProfiles(Transport transports)
    {
        var profiles = new List<ConnectionProfile>(4);
        if ((transports & Transport.Bluetooth) != 0)
        {
            profiles.Add(ConnectionProfile.Bluetooth);
        }
        if ((transports & Transport.Cellular) != 0)
        {
            profiles.Add(ConnectionProfile.Cellular);
        }
        if ((transports & Transport.Ethernet) != 0)
        {
            profiles.Add(ConnectionProfile.Ethernet);
        }
        if ((transports & Transport.WiFi) != 0)
        {
            profiles.Add(ConnectionProfile.WiFi);
        }

        return [.. profiles];
    }

    private sealed class NetworkCallback : ConnectivityManager.NetworkCallback
    {
        private readonly DeviceInformation owner;

        public NetworkCallback(DeviceInformation owner)
        {
            this.owner = owner;
        }

        public override void OnCapabilitiesChanged(AndroidNetwork network, NetworkCapabilities networkCapabilities)
        {
            base.OnCapabilitiesChanged(network, networkCapabilities);
            owner.OnCapabilitiesChanged(network, networkCapabilities);
        }

        public override void OnLost(AndroidNetwork network)
        {
            base.OnLost(network);
            owner.OnLost(network);
        }
    }

    private partial void ReadProcessStat(out int threadCount, out long workingSet)
    {
        threadCount = 0;
        workingSet = 0;

        Span<byte> buffer = stackalloc byte[StatBufferSize];
        var values = buffer[..RandomAccess.Read(statHandle, buffer, 0)];

        values = values[(values.LastIndexOf((byte)')') + 2)..];
        for (var field = 3; field <= ResidentPagesField; field++)
        {
            var end = values.IndexOf((byte)' ');
            var value = end < 0 ? values : values[..end];
            if (field == ThreadCountField)
            {
                threadCount = Utf8Parser.TryParse(value, out int count, out _) ? count : 0;
            }
            else if (field == ResidentPagesField)
            {
                workingSet = Utf8Parser.TryParse(value, out long pages, out _) ? pages * PageSize : 0;
            }

            if (end < 0)
            {
                break;
            }

            values = values[(end + 1)..];
        }
    }
}
