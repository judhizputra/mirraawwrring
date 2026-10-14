using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests;

/// <summary>IAdbClient palsu untuk DeviceWatcher: GetDevicesAsync mengembalikan hasil berurutan dari antrean.</summary>
public sealed class FakeAdbClient : IAdbClient
{
    public Queue<Func<IReadOnlyList<Device>>> DeviceResponses { get; } = new();

    public int GetDevicesCalls { get; private set; }

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
        if (DeviceResponses.Count > 0)
        {
            _lastResponse = DeviceResponses.Dequeue();
        }

        return Task.FromResult(_lastResponse());
    }

    public Task<DeviceDetails> GetDeviceDetailsAsync(string serial, CancellationToken cancellationToken = default) =>
        Task.FromResult(new DeviceDetails("Xiaomi", "2407FPN8EG", "16"));

    public Task<PairResult> PairAsync(string hostPort, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ConnectResult> ConnectAsync(string hostPort, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessResult> DisconnectAsync(string? hostPort = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessResult> TcpIpAsync(string serial, int port = 5555, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string?> GetWifiIpAsync(string serial, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<MdnsService>> GetMdnsServicesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AdbServerStartResult> StartServerAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessResult> KillServerAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ProcessResult> VersionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
}
