namespace Mirraawwrring.Core.Logging;

/// <summary>
/// Menulis log ke file harian <c>Mirraawwrring-yyyyMMdd.log</c> dan menghapus file yang lebih tua dari <see cref="RetentionDays"/> hari.
/// </summary>
public sealed class FileLogWriter : IDisposable
{
    public const string FilePrefix = "Mirraawwrring-";
    public const string FileExtension = ".log";
    public const int RetentionDays = 7;

    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _disposed;

    public FileLogWriter(string directory, TimeProvider? timeProvider = null)
    {
        _directory = directory;
        _time = timeProvider ?? TimeProvider.System;
    }

    public string CurrentFile => Path.Combine(_directory, FileName(Today()));

    public static string FileName(DateOnly date) => $"{FilePrefix}{date:yyyyMMdd}{FileExtension}";

    public void Write(LogEntry entry)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var today = Today();
                if (_writer is null || today != _writerDate)
                {
                    RotateLocked(today);
                }

                _writer!.WriteLine(entry.ToFileLine());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Log tidak boleh menjatuhkan aplikasi.
            }
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    private void RotateLocked(DateOnly today)
    {
        _writer?.Dispose();
        Directory.CreateDirectory(_directory);
        var stream = new FileStream(Path.Combine(_directory, FileName(today)), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        _writerDate = today;
        CleanupOldFiles(_directory, today);
    }

    /// <summary>Hapus file log yang tanggalnya lebih tua dari <paramref name="today"/> dikurangi <see cref="RetentionDays"/>.</summary>
    public static void CleanupOldFiles(string directory, DateOnly today)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        var cutoff = today.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(directory, FilePrefix + "*" + FileExtension))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var datePart = name[FilePrefix.Length..];
            if (DateOnly.TryParseExact(datePart, "yyyyMMdd", out var date) && date < cutoff)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
