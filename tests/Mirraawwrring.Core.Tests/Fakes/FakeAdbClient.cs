using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests;

/// <summary>IAdbClient palsu: GetDevicesAsync mengembalikan hasil berurutan dari antrean; hasil lain bisa diatur lewat properti.</summary>
public sealed class FakeAdbClient : IAdbClient
{
    public Queue<Func<IReadOnlyList<Device>>> DeviceResponses { get; } = new();

    public int GetDevicesCalls { get; private set; }

    public List<string> Calls { get; } = [];

    public PairResult PairResponse { get; set; } = new(false, null, null, "Failed: Unable to start pairing client.");

    public Func<string, ConnectResult> ConnectResponse { get; set; } = addr => new ConnectResult(true, false, addr, $"connected to {addr}");

    public ProcessResult TcpIpResponse { get; set; } = ProcessResult.Ok("restarting in TCP mode port: 5555");

    public string? WifiIp { get; set; } = "192.168.1.20";

    public IReadOnlyList<MdnsService> MdnsResponse { get; set; } = [];

    public Exception? MdnsFailure { get; set; }

    public FakeAdbClient Enqueue(params Device[] devices)
    {
        DeviceResponses.Enqueue(() => devices);
        return this;
    }

    public FakeAdbClient EnqueueFailure(Exception ex)
    {
        DeviceResponses.Enqueue(() => throw ex);
        return this;
    }

    private Func<IReadOnlyList<Device>> _lastResponse = () => [];

    /// <summary>Mengambil dari antrean; kalau antrean habis, jawaban terakhir diulang terus.</summary>
    public Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        GetDevicesCalls++;
        Calls.Add("devices");
        if (DeviceResponses.Count > 0)
        {
            _lastResponse = DeviceResponses.Dequeue();
        }

        return Task.FromResult(_lastResponse());
    }

    public Task<DeviceDetails> GetDeviceDetailsAsync(string serial, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeviceDetails("Xiaomi", "2407FPN8EG", "16"));

    public Task<PairResult> PairAsync(string hostPort, string code, CancellationToken cancellationToken = default)
    {
        Calls.Add($"pair {hostPort} {code}");
        return Task.FromResult(PairResponse);
    }

    public Task<ConnectResult> ConnectAsync(string hostPort, CancellationToken cancellationToken = default)
    {
        Calls.Add($"connect {hostPort}");
        return Task.FromResult(ConnectResponse(hostPort));
    }

    public Task<ProcessResult> DisconnectAsync(string? hostPort = null, CancellationToken cancellationToken = default)
    {
        Calls.Add($"disconnect {hostPort}");
        return Task.FromResult(ProcessResult.Ok());
    }

    public Task<ProcessResult> TcpIpAsync(string serial, int port = 5555, CancellationToken cancellationToken = default)
    {
        Calls.Add($"tcpip {serial} {port}");
        return Task.FromResult(TcpIpResponse);
    }

    public Task<string?> GetWifiIpAsync(string serial, CancellationToken cancellationToken = default)
    {
        Calls.Add($"wifi-ip {serial}");
        return Task.FromResult(WifiIp);
    }

    public Task<IReadOnlyList<MdnsService>> GetMdnsServicesAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add("mdns");
        return MdnsFailure is not null ? Task.FromException<IReadOnlyList<MdnsService>>(MdnsFailure) : Task.FromResult(MdnsResponse);
    }

    public Task<AdbServerStartResult> StartServerAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AdbServerStartResult(true, false, false, ""));
    public Task<ProcessResult> KillServerAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProcessResult.Ok());
    public Task<ProcessResult> VersionAsync(CancellationToken cancellationToken = default) => Task.FromResult(ProcessResult.Ok("Android Debug Bridge version 1.0.41"));
    public Task<ProcessResult> ShellAsync(string serial, IReadOnlyList<string> command, TimeSpan? timeout = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout = null, bool binaryStdout = false, bool quiet = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

public static class TestDevices
{
    public static Device Usb(string serial = "R58N12ABCDE", DeviceState state = DeviceState.Device, string? model = "SM G998B") => new()
    {
        Serial = serial,
        State = state,
        RawState = state.ToString().ToLowerInvariant(),
        Transport = DeviceTransport.Usb,
        Model = model,
    };

    public static Device Wifi(string serial = "192.168.1.20:41235", DeviceState state = DeviceState.Device) => new()
    {
        Serial = serial,
        State = state,
        RawState = state.ToString().ToLowerInvariant(),
        Transport = DeviceTransport.WifiIp,
        Model = "2407FPN8EG",
    };

    public static Device Mdns(string name = "adb-FQYDJFRWIZGUZ989-8Usjcv") => new()
    {
        Serial = name + "._adb-tls-connect._tcp",
        State = DeviceState.Device,
        RawState = "device",
        Transport = DeviceTransport.WifiMdns,
        Model = "2407FPN8EG",
    };
}

/// <summary>ISettingsStore di memori untuk test.</summary>
public sealed class FakeSettingsStore : Core.Settings.ISettingsStore
{
    public Core.Settings.AppSettings Current { get; } = new();

    public int Saves { get; private set; }

    public void Update(Action<Core.Settings.AppSettings> change)
    {
        change(Current);
        Saves++;
    }

    public void Save() => Saves++;
}
