using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Adb;

/// <summary>Implementasi <see cref="IAdbClient"/> di atas <see cref="IProcessRunner"/>; path adb diambil dari <see cref="IToolRegistry"/>.</summary>
public sealed class AdbClient(IProcessRunner runner, IToolRegistry tools, ILogger<AdbClient> logger) : IAdbClient
{
    public const int DefaultTcpIpPort = 5555;

    /// <summary>Pairing dan connect butuh handshake TLS; beri waktu lebih.</summary>
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["devices", "-l"], quiet: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return AdbDevicesParser.Parse(result.StandardOutput);
    }

    public async Task<DeviceDetails> GetDeviceDetailsAsync(string serial, CancellationToken cancellationToken = default)
    {
        var manufacturer = await GetPropAsync(serial, "ro.product.manufacturer", cancellationToken).ConfigureAwait(false);
        var model = await GetPropAsync(serial, "ro.product.model", cancellationToken).ConfigureAwait(false);
        var release = await GetPropAsync(serial, "ro.build.version.release", cancellationToken).ConfigureAwait(false);
        return new DeviceDetails(manufacturer, model, release);
    }

    public async Task<PairResult> PairAsync(string hostPort, string code, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["pair", hostPort.Trim(), code.Trim()], NetworkTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
        {
            return new PairResult(false, null, null, "adb pair tidak merespons (timeout). Pastikan HP dan PC di jaringan yang sama.");
        }

        return AdbPairResultParser.Parse(result.CombinedOutput);
    }

    public async Task<ConnectResult> ConnectAsync(string hostPort, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["connect", hostPort.Trim()], NetworkTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result.TimedOut)
        {
            return new ConnectResult(false, false, null, "adb connect tidak merespons (timeout).");
        }

        return AdbConnectResultParser.Parse(result.CombinedOutput);
    }

    public Task<ProcessResult> DisconnectAsync(string? hostPort = null, CancellationToken cancellationToken = default)
    {
        var args = hostPort is null ? new[] { "disconnect" } : ["disconnect", hostPort.Trim()];
        return RunAsync(args, cancellationToken: cancellationToken);
    }

    public Task<ProcessResult> TcpIpAsync(string serial, int port = DefaultTcpIpPort, CancellationToken cancellationToken = default) =>
        RunAsync(["-s", serial, "tcpip", port.ToString(System.Globalization.CultureInfo.InvariantCulture)], cancellationToken: cancellationToken);

    public async Task<string?> GetWifiIpAsync(string serial, CancellationToken cancellationToken = default)
    {
        var addr = await ShellAsync(serial, ["ip", "-f", "inet", "addr", "show", "wlan0"], cancellationToken: cancellationToken).ConfigureAwait(false);
        var ip = WifiIpParser.ParseInetAddr(addr.StandardOutput);
        if (ip is not null)
        {
            return ip;
        }

        var route = await ShellAsync(serial, ["ip", "route"], cancellationToken: cancellationToken).ConfigureAwait(false);
        return WifiIpParser.ParseRouteSource(route.StandardOutput);
    }

    public async Task<IReadOnlyList<MdnsService>> GetMdnsServicesAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["mdns", "services"], quiet: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        return MdnsServicesParser.Parse(result.StandardOutput);
    }

    public async Task<AdbServerStartResult> StartServerAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["start-server"], TimeSpan.FromSeconds(20), cancellationToken: cancellationToken).ConfigureAwait(false);
        var parsed = AdbServerOutputParser.Parse(result.CombinedOutput, result.TimedOut ? -1 : result.ExitCode);
        if (parsed.VersionMismatch)
        {
            logger.LogWarning("Versi server adb tidak cocok dengan client; kemungkinan ada adb lain yang berjalan");
        }

        return parsed;
    }

    public Task<ProcessResult> KillServerAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["kill-server"], cancellationToken: cancellationToken);

    public Task<ProcessResult> VersionAsync(CancellationToken cancellationToken = default) =>
        RunAsync(["version"], cancellationToken: cancellationToken);

    public Task<ProcessResult> ShellAsync(string serial, IReadOnlyList<string> command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(command.Count);
        return RunAsync(["-s", serial, "shell", ShellQuoting.Join(command)], timeout, cancellationToken: cancellationToken);
    }

    public Task<ProcessResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan? timeout = null, bool binaryStdout = false, bool quiet = false, CancellationToken cancellationToken = default)
    {
        var request = new ProcessRequest
        {
            FileName = tools.RequireAdb(),
            Arguments = arguments,
            Timeout = timeout ?? ProcessRequest.DefaultTimeout,
            BinaryStdout = binaryStdout,
            Quiet = quiet,
        };
        return runner.RunAsync(request, cancellationToken);
    }

    private async Task<string?> GetPropAsync(string serial, string property, CancellationToken cancellationToken)
    {
        var result = await ShellAsync(serial, ["getprop", property], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        var value = result.StandardOutput.Trim();
        return value.Length == 0 ? null : value;
    }
}
