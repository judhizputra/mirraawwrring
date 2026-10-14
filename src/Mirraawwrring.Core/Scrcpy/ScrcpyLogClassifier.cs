using System.Text.RegularExpressions;

namespace Mirraawwrring.Core.Scrcpy;

public enum ScrcpyLogLevel
{
    Info,
    Warning,
    Error,
    Debug,

    /// <summary>Baris tanpa prefix level (mis. baris versi, output lain).</summary>
    Other,
}

/// <summary>Pesan penting yang ditindaklanjuti UI (§5.6).</summary>
public enum ScrcpyLogKind
{
    None,

    /// <summary><c>scrcpy 4.1 &lt;https://…&gt;</c>; <see cref="ScrcpyLogLine.Detail"/> = versi.</summary>
    Version,

    /// <summary><c>INFO: ADB device found:</c></summary>
    DeviceFound,

    /// <summary><c>[server] INFO: Device: [Xiaomi] Xiaomi 2407FPN8EG (Android 16)</c>; Detail = teks setelah "Device: ".</summary>
    DeviceInfo,

    /// <summary>INJECT_EVENTS: input diblokir (HyperOS Security settings). Tampilkan banner + sarankan UHID.</summary>
    InjectionBlocked,

    /// <summary><c>ERROR: Could not find any ADB device</c></summary>
    NoDevice,

    /// <summary><c>ERROR: Server connection failed</c></summary>
    ServerConnectionFailed,

    /// <summary><c>WARN: Device disconnected</c></summary>
    DeviceDisconnected,

    /// <summary>Error lain yang menyebut server (gagal push/start server, server berhenti mendadak).</summary>
    ServerError,

    /// <summary><c>INFO: Renderer: direct3d11</c> dan <c>INFO: Texture: WxH</c>: sesi sudah tampil.</summary>
    Rendering,
}

public sealed record ScrcpyLogLine(string Raw, ScrcpyLogLevel Level, ScrcpyLogKind Kind, bool FromServer, string Message, string? Detail = null)
{
    public bool IsProblem => Level == ScrcpyLogLevel.Error || Kind is ScrcpyLogKind.InjectionBlocked or ScrcpyLogKind.DeviceDisconnected;
}

/// <summary>Fungsi murni: mengenali baris output scrcpy (§5.6). Contoh nyata ada di PROMPT.md §11.</summary>
public static partial class ScrcpyLogClassifier
{
    [GeneratedRegex(@"^(?<server>\[server\]\s+)?(?:(?<level>INFO|WARN|ERROR|DEBUG):\s*)?(?<msg>.*)$")]
    private static partial Regex LineRegex();

    [GeneratedRegex(@"^scrcpy\s+(?<version>\d+(?:\.\d+)+)\b")]
    private static partial Regex VersionRegex();

    public static ScrcpyLogLine Classify(string line)
    {
        var raw = line.TrimEnd('\r', '\n');
        var m = LineRegex().Match(raw.Trim());
        var fromServer = m.Groups["server"].Success;
        var level = m.Groups["level"].Success ? ParseLevel(m.Groups["level"].Value) : ScrcpyLogLevel.Other;
        var message = m.Groups["msg"].Value.Trim();

        var (kind, detail) = Detect(level, message);
        return new ScrcpyLogLine(raw, level, kind, fromServer, message, detail);
    }

    private static (ScrcpyLogKind Kind, string? Detail) Detect(ScrcpyLogLevel level, string message)
    {
        if (message.Contains("INJECT_EVENTS", StringComparison.Ordinal)
            || message.Contains("USB debugging (Security Settings)", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.InjectionBlocked, null);
        }

        if (message.StartsWith("Could not find any ADB device", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.NoDevice, null);
        }

        if (message.StartsWith("Server connection failed", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.ServerConnectionFailed, null);
        }

        if (message.StartsWith("Device disconnected", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.DeviceDisconnected, null);
        }

        var version = VersionRegex().Match(message);
        if (version.Success)
        {
            return (ScrcpyLogKind.Version, version.Groups["version"].Value);
        }

        if (message.StartsWith("Device: ", StringComparison.Ordinal))
        {
            return (ScrcpyLogKind.DeviceInfo, message["Device: ".Length..].Trim());
        }

        if (message.StartsWith("ADB device found", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.DeviceFound, null);
        }

        if (message.StartsWith("Renderer: ", StringComparison.Ordinal) || message.StartsWith("Texture: ", StringComparison.Ordinal))
        {
            return (ScrcpyLogKind.Rendering, message[(message.IndexOf(':') + 1)..].Trim());
        }

        if (level == ScrcpyLogLevel.Error && message.Contains("server", StringComparison.OrdinalIgnoreCase))
        {
            return (ScrcpyLogKind.ServerError, null);
        }

        return (ScrcpyLogKind.None, null);
    }

    private static ScrcpyLogLevel ParseLevel(string level) => level switch
    {
        "INFO" => ScrcpyLogLevel.Info,
        "WARN" => ScrcpyLogLevel.Warning,
        "ERROR" => ScrcpyLogLevel.Error,
        "DEBUG" => ScrcpyLogLevel.Debug,
        _ => ScrcpyLogLevel.Other,
    };
}
