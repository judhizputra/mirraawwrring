using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Settings;

namespace Mirraawwrring.Core.Wireless;

/// <summary>Hasil pairing + usaha koneksi otomatis.</summary>
public sealed record PairOutcome(bool Paired, string? ConnectedSerial, string Message)
{
    /// <summary>Pairing sukses tetapi adb belum tersambung otomatis; minta port koneksi dari pengguna.</summary>
    public bool NeedsConnectPort => Paired && ConnectedSerial is null;
}

public sealed record WifiEnableOutcome(bool Success, string? Address, string Message);

/// <summary>
/// Alur koneksi nirkabel (§6.3, §6.4, prototipe scrcpy-launcher.ps1):
/// pair → tunggu koneksi otomatis via mDNS maks 5 detik → kalau belum, connect manual ke port koneksi.
/// </summary>
public sealed class WirelessService(IAdbClient adb, ISettingsStore settings, ILogger<WirelessService> logger, TimeProvider? timeProvider = null)
{
    public const int MaxHistory = 10;
    public static readonly TimeSpan AutoConnectTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan AutoConnectPollInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan TcpIpRestartDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan QrPollInterval = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public IReadOnlyList<string> ConnectionHistory => settings.Current.ConnectionHistory.ToArray();

    /// <summary><c>adb pair</c> lalu tunggu perangkat muncul (serial <c>host:*</c> atau <c>adb-*</c>) sampai <see cref="AutoConnectTimeout"/>.</summary>
    public async Task<PairOutcome> PairAsync(string pairingHostPort, string code, CancellationToken cancellationToken = default)
    {
        if (!AddressUtil.TryParseHostPort(pairingHostPort, out var host, out _))
        {
            return new PairOutcome(false, null, "Format harus IP:port, sesuai yang tampil di dialog HP.");
        }

        var normalizedCode = AddressUtil.NormalizePairingCode(code);
        if (normalizedCode is null)
        {
            return new PairOutcome(false, null, "Kode penyambungan harus 6 digit.");
        }

        return await PairWithPasswordAsync(pairingHostPort, normalizedCode, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>adb pair host:port password</c> tanpa aturan 6 digit: dipakai QR pairing (password acak dari QR) dan oleh <see cref="PairAsync"/>.
    /// </summary>
    public async Task<PairOutcome> PairWithPasswordAsync(string pairingHostPort, string password, CancellationToken cancellationToken = default)
    {
        if (!AddressUtil.TryParseHostPort(pairingHostPort, out var host, out _))
        {
            return new PairOutcome(false, null, "Format harus IP:port, sesuai yang tampil di dialog HP.");
        }

        var pair = await adb.PairAsync(pairingHostPort.Trim(), password, cancellationToken).ConfigureAwait(false);
        if (!pair.Success)
        {
            logger.LogWarning("Pairing gagal: {Message}", pair.Message);
            return new PairOutcome(false, null, pair.Message);
        }

        logger.LogInformation("Pairing berhasil dengan {Address}", pair.Address);
        var serial = await WaitForAutoConnectAsync(host, cancellationToken).ConfigureAwait(false);
        return serial is null
            ? new PairOutcome(true, null, pair.Message)
            : new PairOutcome(true, serial, pair.Message);
    }

    /// <summary>
    /// QR pairing: poll <c>adb mdns services</c> sampai HP (yang sudah memindai QR) mengumumkan layanan pairing bernama
    /// <paramref name="serviceName"/>, lalu kembalikan layanannya. Null jika <paramref name="timeout"/> habis.
    /// </summary>
    public async Task<MdnsService?> WaitForPairingServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var deadline = _time.GetUtcNow() + timeout;
        while (true)
        {
            IReadOnlyList<MdnsService> services;
            try
            {
                services = await adb.GetMdnsServicesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "adb mdns services gagal saat menunggu QR pairing");
                services = [];
            }

            var found = QrPairing.FindPairingService(services, serviceName);
            if (found is not null)
            {
                logger.LogInformation("HP memindai QR: layanan {Name} di {Address}", found.Name, found.Address);
                return found;
            }

            if (_time.GetUtcNow() >= deadline)
            {
                return null;
            }

            await Task.Delay(QrPollInterval, _time, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Poll <c>adb devices</c> tiap 1 detik; adb biasanya langsung tersambung lewat mDNS setelah pairing.</summary>
    public async Task<string?> WaitForAutoConnectAsync(string host, CancellationToken cancellationToken = default)
    {
        var deadline = _time.GetUtcNow() + AutoConnectTimeout;
        while (true)
        {
            await Task.Delay(AutoConnectPollInterval, _time, cancellationToken).ConfigureAwait(false);
            var devices = await adb.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            var match = devices.FirstOrDefault(d => d.IsReady && IsWirelessMatch(d, host));
            if (match is not null)
            {
                logger.LogInformation("Tersambung otomatis: {Serial}", match.Serial);
                return match.Serial;
            }

            if (_time.GetUtcNow() >= deadline)
            {
                return null;
            }
        }
    }

    /// <summary><c>adb connect</c>; port default 5555. Alamat sukses dicatat ke riwayat.</summary>
    public async Task<ConnectResult> ConnectAsync(string hostPortOrHost, CancellationToken cancellationToken = default)
    {
        var address = AddressUtil.NormalizeConnectAddress(hostPortOrHost);
        if (address is null)
        {
            return new ConnectResult(false, false, null, "Alamat tidak valid. Contoh: 192.168.1.20:5555");
        }

        var result = await adb.ConnectAsync(address, cancellationToken).ConfigureAwait(false);
        if (result.Success)
        {
            RememberAddress(result.Address ?? address);
        }
        else
        {
            logger.LogWarning("Connect gagal: {Message}", result.Message);
        }

        return result;
    }

    /// <summary>Dari HP yang tersambung USB: ambil IP Wi-Fi, <c>adb tcpip 5555</c>, lalu <c>adb connect ip:5555</c>.</summary>
    public async Task<WifiEnableOutcome> EnableWifiFromUsbAsync(string usbSerial, CancellationToken cancellationToken = default)
    {
        var ip = await adb.GetWifiIpAsync(usbSerial, cancellationToken).ConfigureAwait(false);
        if (ip is null)
        {
            return new WifiEnableOutcome(false, null, "IP Wi-Fi HP tidak ditemukan. Pastikan Wi-Fi HP menyala dan tersambung ke jaringan yang sama.");
        }

        var tcpip = await adb.TcpIpAsync(usbSerial, AddressUtil.DefaultConnectPort, cancellationToken).ConfigureAwait(false);
        if (!tcpip.Succeeded)
        {
            return new WifiEnableOutcome(false, null, tcpip.CombinedOutput.Length > 0 ? tcpip.CombinedOutput : "adb tcpip gagal.");
        }

        await Task.Delay(TcpIpRestartDelay, _time, cancellationToken).ConfigureAwait(false);
        var address = $"{ip}:{AddressUtil.DefaultConnectPort}";
        var connect = await ConnectAsync(address, cancellationToken).ConfigureAwait(false);
        return new WifiEnableOutcome(connect.Success, connect.Success ? address : null, connect.Message);
    }

    public void RememberAddress(string address)
    {
        settings.Update(s =>
        {
            s.ConnectionHistory.RemoveAll(a => string.Equals(a, address, StringComparison.OrdinalIgnoreCase));
            s.ConnectionHistory.Insert(0, address);
            if (s.ConnectionHistory.Count > MaxHistory)
            {
                s.ConnectionHistory.RemoveRange(MaxHistory, s.ConnectionHistory.Count - MaxHistory);
            }
        });
    }

    public void ForgetAddress(string address)
    {
        settings.Update(s => s.ConnectionHistory.RemoveAll(a => string.Equals(a, address, StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool IsWirelessMatch(Device device, string host) =>
        device.Transport == DeviceTransport.WifiMdns
        || (device.Transport == DeviceTransport.WifiIp && string.Equals(device.Host, host, StringComparison.OrdinalIgnoreCase));
}
