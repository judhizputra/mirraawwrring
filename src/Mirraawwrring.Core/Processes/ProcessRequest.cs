using System.Text;

namespace Mirraawwrring.Core.Processes;

/// <summary>Deskripsi satu perintah eksternal (adb, scrcpy, winget) yang akan dijalankan lewat <see cref="IProcessRunner"/>.</summary>
public sealed record ProcessRequest
{
    /// <summary>Timeout bawaan untuk perintah adb biasa (prinsip §4.3).</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public required string FileName { get; init; }

    /// <summary>Argumen mentah, tanpa quoting; quoting Windows ditangani ProcessStartInfo.ArgumentList.</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public string? WorkingDirectory { get; init; }

    /// <summary>True untuk membaca stdout sebagai byte mentah (mis. <c>adb exec-out screencap -p</c>).</summary>
    public bool BinaryStdout { get; init; }

    public static ProcessRequest Create(string fileName, params string[] arguments) =>
        new() { FileName = fileName, Arguments = arguments };

    /// <summary>Representasi untuk log: nama file tanpa folder + argumen (yang mengandung spasi diberi tanda kutip).</summary>
    public string ToLogString()
    {
        var sb = new StringBuilder(Path.GetFileNameWithoutExtension(FileName));
        foreach (var arg in Arguments)
        {
            sb.Append(' ');
            sb.Append(arg.Contains(' ') || arg.Length == 0 ? $"\"{arg}\"" : arg);
        }

        return sb.ToString();
    }
}

/// <summary>Satu baris output dari proses yang berjalan lama (scrcpy).</summary>
public readonly record struct ProcessOutputLine(bool IsError, string Text);
