using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Mirraawwrring.Core.Wireless;

/// <summary>Validasi dan normalisasi alamat <c>host:port</c> dan kode pairing. Fungsi murni.</summary>
public static partial class AddressUtil
{
    public const int DefaultConnectPort = 5555;

    [GeneratedRegex(@"^(?<host>[A-Za-z0-9.\-]+)(?::(?<port>\d{1,5}))?$")]
    private static partial Regex HostPortRegex();

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex CodeRegex();

    /// <summary>Pisahkan "192.168.1.20:37099" menjadi host dan port. Port wajib ada dan 1..65535.</summary>
    public static bool TryParseHostPort(string? input, [NotNullWhen(true)] out string? host, out int port)
    {
        host = null;
        port = 0;
        var m = HostPortRegex().Match((input ?? string.Empty).Trim());
        if (!m.Success || !m.Groups["port"].Success)
        {
            return false;
        }

        if (!int.TryParse(m.Groups["port"].Value, out port) || port is < 1 or > 65535)
        {
            return false;
        }

        host = m.Groups["host"].Value;
        return true;
    }

    /// <summary>"192.168.1.20" → "192.168.1.20:5555"; "192.168.1.20:41235" tetap. null jika tidak valid.</summary>
    public static string? NormalizeConnectAddress(string? input, int defaultPort = DefaultConnectPort)
    {
        var text = (input ?? string.Empty).Trim();
        var m = HostPortRegex().Match(text);
        if (!m.Success)
        {
            return null;
        }

        var port = defaultPort;
        if (m.Groups["port"].Success && (!int.TryParse(m.Groups["port"].Value, out port) || port is < 1 or > 65535))
        {
            return null;
        }

        return $"{m.Groups["host"].Value}:{port}";
    }

    /// <summary>Kode pairing: hanya digit, spasi/tanda hubung diabaikan, harus tepat 6 digit.</summary>
    public static string? NormalizePairingCode(string? input)
    {
        var digits = new string((input ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return CodeRegex().IsMatch(digits) ? digits : null;
    }

    public static string HostOf(string hostPort)
    {
        var colon = hostPort.LastIndexOf(':');
        return colon > 0 ? hostPort[..colon] : hostPort;
    }
}
