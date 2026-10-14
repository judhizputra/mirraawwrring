using System.Text.RegularExpressions;

namespace Mirraawwrring.Core.Adb;

/// <summary>Hasil <c>adb pair</c>.</summary>
public sealed record PairResult(bool Success, string? Address, string? Guid, string Message);

/// <summary>Parser murni output <c>adb pair host:port kode</c>.</summary>
public static partial class AdbPairResultParser
{
    [GeneratedRegex(@"Successfully paired to (?<addr>\S+)(?:\s+\[guid=(?<guid>[^\]]+)\])?", RegexOptions.IgnoreCase)]
    private static partial Regex SuccessRegex();

    public static PairResult Parse(string output)
    {
        var text = output.Trim();
        var match = SuccessRegex().Match(text);
        if (match.Success)
        {
            var guid = match.Groups["guid"].Success ? match.Groups["guid"].Value : null;
            return new PairResult(true, match.Groups["addr"].Value, guid, text);
        }

        return new PairResult(false, null, null, text.Length == 0 ? "Tidak ada respons dari adb." : text);
    }
}

/// <summary>Hasil <c>adb connect</c>.</summary>
public sealed record ConnectResult(bool Success, bool AlreadyConnected, string? Address, string Message);

/// <summary>Parser murni output <c>adb connect host:port</c>.</summary>
public static partial class AdbConnectResultParser
{
    [GeneratedRegex(@"^(?<already>already )?connected to (?<addr>\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SuccessRegex();

    public static ConnectResult Parse(string output)
    {
        var text = output.Trim();
        var match = SuccessRegex().Match(text);
        if (match.Success)
        {
            return new ConnectResult(true, match.Groups["already"].Success, match.Groups["addr"].Value, text);
        }

        return new ConnectResult(false, false, null, text.Length == 0 ? "Tidak ada respons dari adb." : text);
    }
}

/// <summary>Hasil <c>adb start-server</c> termasuk deteksi bentrok versi (§5.2).</summary>
public sealed record AdbServerStartResult(bool Success, bool VersionMismatch, bool PortInUse, string Message);

public static class AdbServerOutputParser
{
    public static AdbServerStartResult Parse(string output, int exitCode)
    {
        var text = output.Trim();
        var mismatch = text.Contains("doesn't match this client", StringComparison.OrdinalIgnoreCase);
        var portInUse = text.Contains("could not install *smartsocket* listener", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Address already in use", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase);
        var success = exitCode == 0 && !portInUse;
        return new AdbServerStartResult(success, mismatch, portInUse, text);
    }
}

/// <summary>Layanan mDNS dari <c>adb mdns services</c>.</summary>
public sealed record MdnsService(string Name, string ServiceType, string Host, int Port)
{
    public const string ConnectType = "_adb-tls-connect._tcp";
    public const string PairingType = "_adb-tls-pairing._tcp";
    public const string LegacyType = "_adb._tcp";

    public bool IsConnectService => ServiceType.Equals(ConnectType, StringComparison.OrdinalIgnoreCase);

    public bool IsPairingService => ServiceType.Equals(PairingType, StringComparison.OrdinalIgnoreCase);

    public string Address => $"{Host}:{Port}";
}

/// <summary>
/// Parser murni output <c>adb mdns services</c>:
/// <code>
/// List of discovered mdns services
/// adb-FQYDJFRWIZGUZ989-8Usjcv	_adb-tls-connect._tcp	192.168.1.20:41235
/// </code>
/// </summary>
public static class MdnsServicesParser
{
    public static IReadOnlyList<MdnsService> Parse(string output)
    {
        var result = new List<MdnsService>();
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("List of", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                continue;
            }

            var address = parts[2];
            var colon = address.LastIndexOf(':');
            if (colon <= 0 || !int.TryParse(address[(colon + 1)..], out var port))
            {
                continue;
            }

            result.Add(new MdnsService(parts[0], parts[1], address[..colon], port));
        }

        return result;
    }
}

/// <summary>Mengambil IPv4 HP dari output <c>ip</c> di shell Android.</summary>
public static partial class WifiIpParser
{
    [GeneratedRegex(@"\binet (?<ip>\d{1,3}(?:\.\d{1,3}){3})/\d+")]
    private static partial Regex InetRegex();

    [GeneratedRegex(@"\bsrc (?<ip>\d{1,3}(?:\.\d{1,3}){3})\b")]
    private static partial Regex RouteSrcRegex();

    /// <summary>Dari <c>ip -f inet addr show wlan0</c>: baris <c>inet 192.168.1.20/24 …</c>.</summary>
    public static string? ParseInetAddr(string output)
    {
        var m = InetRegex().Match(output);
        return m.Success ? m.Groups["ip"].Value : null;
    }

    /// <summary>Dari <c>ip route</c>: <c>… src 192.168.1.20</c>; dipakai kalau antarmuka bukan wlan0.</summary>
    public static string? ParseRouteSource(string output)
    {
        var m = RouteSrcRegex().Match(output);
        return m.Success ? m.Groups["ip"].Value : null;
    }
}
