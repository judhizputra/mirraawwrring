using Microsoft.Extensions.Logging;

namespace Mirraawwrring.Core.Logging;

/// <summary>
/// Provider ILogger yang meneruskan semua log ke <see cref="LogBuffer"/> (panel Log) dan <see cref="FileLogWriter"/> (file harian).
/// Didaftarkan sekali di DI (App.xaml.cs).
/// </summary>
public sealed class AppLoggerProvider : ILoggerProvider
{
    private readonly LogBuffer _buffer;
    private readonly FileLogWriter? _file;
    private readonly TimeProvider _time;
    private readonly LogLevel _minimumLevel;

    public AppLoggerProvider(LogBuffer buffer, FileLogWriter? file, LogLevel minimumLevel = LogLevel.Debug, TimeProvider? timeProvider = null)
    {
        _buffer = buffer;
        _file = file;
        _minimumLevel = minimumLevel;
        _time = timeProvider ?? TimeProvider.System;
    }

    public ILogger CreateLogger(string categoryName) => new AppLogger(this, ShortCategory(categoryName));

    public void Dispose() => _file?.Dispose();

    /// <summary>"Mirraawwrring.Core.Adb.AdbClient" menjadi "AdbClient" supaya panel Log ringkas.</summary>
    internal static string ShortCategory(string category)
    {
        var dot = category.LastIndexOf('.');
        return dot >= 0 ? category[(dot + 1)..] : category;
    }

    private sealed class AppLogger(AppLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= owner._minimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var entry = new LogEntry(owner._time.GetLocalNow(), logLevel, category, formatter(state, exception), exception);
            owner._buffer.Add(entry);
            owner._file?.Write(entry);
        }
    }
}
