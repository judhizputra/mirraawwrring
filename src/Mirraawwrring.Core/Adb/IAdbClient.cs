using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Adb;

/// <summary>Pembungkus perintah adb (§5.2). Semua metode async dengan timeout; tidak melempar untuk exit code bukan nol.</summary>
public interface IAdbClient
{
    Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<DeviceDetails> GetDeviceDetailsAsync(string serial, CancellationToken cancellationToken = default);

    Task<PairResult> PairAsync(string hostPort, string code, CancellationToken cancellationToken = default);

    Task<ConnectResult> ConnectAsync(string hostPort, CancellationToken cancellationToken = default);

    Task<ProcessResult> DisconnectAsync(string? hostPort = null, CancellationToken cancellationToken = default);

    /// <summary><c>adb -s serial tcpip port</c>: HP mendengarkan koneksi adb lewat Wi-Fi.</summary>
    Task<ProcessResult> TcpIpAsync(string serial, int port = AdbClient.DefaultTcpIpPort, CancellationToken cancellationToken = default);

    /// <summary>IPv4 Wi-Fi HP (wlan0), atau null jika tidak ketemu.</summary>
    Task<string?> GetWifiIpAsync(string serial, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MdnsService>> GetMdnsServicesAsync(CancellationToken cancellationToken = default);

    Task<AdbServerStartResult> StartServerAsync(CancellationToken cancellationToken = default);

    Task<ProcessResult> KillServerAsync(CancellationToken cancellationToken = default);

    /// <summary><c>adb version</c>; baris pertama hasilnya mis. "Android Debug Bridge version 1.0.41".</summary>
    Task<ProcessResult> VersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Jalankan perintah di shell HP. Argumen di-escape lewat <see cref="ShellQuoting"/>.</summary>
    Task<ProcessResult> ShellAsync(string serial, IReadOnlyList<string> command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Jalankan adb dengan argumen mentah (install, pull, push, exec-out, …).</summary>
    Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout = null, bool binaryStdout = false, CancellationToken cancellationToken = default);
}
