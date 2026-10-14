using Microsoft.Extensions.Logging;
using Mirraawwrring.Core.Logging;

namespace Mirraawwrring.Core.Tests.Logging;

public sealed class FileLogWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mirraawwrring-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Write_CreatesDailyFileWithFormattedLine()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 16, 45, 12, TimeSpan.FromHours(7)));
        using var writer = new FileLogWriter(_dir, time);

        writer.Write(new LogEntry(time.GetLocalNow(), LogLevel.Information, "AdbClient", "> adb devices -l"));

        var file = Path.Combine(_dir, "Mirraawwrring-20261014.log");
        Assert.True(File.Exists(file));
        writer.Dispose();
        var content = File.ReadAllText(file);
        Assert.Contains("2026-10-14 16:45:12.000 [INF] AdbClient: > adb devices -l", content);
    }

    [Fact]
    public void CleanupOldFiles_DeletesOnlyFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_dir);
        var today = new DateOnly(2026, 10, 14);
        var old = Path.Combine(_dir, FileLogWriter.FileName(today.AddDays(-8)));
        var edge = Path.Combine(_dir, FileLogWriter.FileName(today.AddDays(-7)));
        var recent = Path.Combine(_dir, FileLogWriter.FileName(today.AddDays(-1)));
        var foreign = Path.Combine(_dir, "lain.log");
        foreach (var f in new[] { old, edge, recent, foreign })
        {
            File.WriteAllText(f, "x");
        }

        FileLogWriter.CleanupOldFiles(_dir, today);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(edge));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(foreign));
    }

    [Fact]
    public void LogBuffer_DropsOldestWhenFull()
    {
        var buffer = new LogBuffer(capacity: 2);
        var raised = 0;
        buffer.EntryAdded += _ => raised++;

        for (var i = 0; i < 3; i++)
        {
            buffer.Add(new LogEntry(DateTimeOffset.Now, LogLevel.Information, "T", $"m{i}"));
        }

        var snapshot = buffer.Snapshot();
        Assert.Equal(3, raised);
        Assert.Equal(["m1", "m2"], snapshot.Select(e => e.Message));
    }

    [Fact]
    public void AppLoggerProvider_ShortensCategoryAndForwardsToBuffer()
    {
        var buffer = new LogBuffer();
        using var provider = new AppLoggerProvider(buffer, file: null, minimumLevel: LogLevel.Information);
        var logger = provider.CreateLogger("Mirraawwrring.Core.Adb.AdbClient");

        logger.LogDebug("diabaikan");
        logger.LogWarning("peringatan {X}", 1);

        var entry = Assert.Single(buffer.Snapshot());
        Assert.Equal("AdbClient", entry.Category);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("peringatan 1", entry.Message);
    }
}
