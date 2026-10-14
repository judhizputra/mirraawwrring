using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Logging;

/// <summary>Satu baris log, dipakai oleh panel Log di UI dan file log harian.</summary>
public sealed record LogEntry(DateTimeOffset Timestamp, LogLevel Level, string Category, string Message, Exception? Exception = null)
{
    public string LevelTag => Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    /// <summary>Format baris untuk file log.</summary>
    public string ToFileLine()
    {
        var line = $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LevelTag}] {Category}: {Message}";
        return Exception is null ? line : line + Environment.NewLine + Exception;
    }
}
