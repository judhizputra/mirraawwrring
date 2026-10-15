using System.Security.Cryptography;
using Mirraawwrring.Core.Adb;

namespace Mirraawwrring.Core.Wireless;

/// <summary>
/// Satu sesi QR pairing (§6.3, fitur Fase 8). Isi QR mengikuti format yang dipakai Android Studio:
/// <c>WIFI:T:ADB;S:&lt;nama layanan&gt;;P:&lt;password&gt;;;</c>. Setelah HP memindainya, HP mengumumkan layanan mDNS
/// <c>_adb-tls-pairing._tcp</c> dengan nama itu; PC lalu menjalankan <c>adb pair host:port password</c>.
/// </summary>
public sealed record QrPairingSession(string ServiceName, string Password)
{
    public string QrContent => $"WIFI:T:ADB;S:{ServiceName};P:{Password};;";
}

public static class QrPairing
{
    public const string ServicePrefix = "mirraawwrring-";
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public static QrPairingSession Create() => new(ServicePrefix + RandomToken(8), RandomToken(12));

    /// <summary>Layanan pairing yang namanya cocok dengan sesi QR; null jika HP belum memindai.</summary>
    public static MdnsService? FindPairingService(IEnumerable<MdnsService> services, string serviceName) =>
        services.FirstOrDefault(s => s.IsPairingService && string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Token acak dari huruf/angka yang tidak membingungkan (tanpa 0/O/1/l/I) dan aman di dalam format QR (tanpa ';' ':').</summary>
    public static string RandomToken(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
