using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Files;
using Mirraawwrring.Core.Processes;

namespace Mirraawwrring.Core.Tests.Files;

public sealed class RemoteFileServiceTests : IDisposable
{
    private const string Serial = "R58N12ABCDE";
    private readonly FakeAdbClient _adb = new();
    private readonly RemoteFileService _service;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "mirraawwrring-files-" + Guid.NewGuid().ToString("N"));

    public RemoteFileServiceTests()
    {
        _service = new RemoteFileService(_adb, new FakeAppPaths { DownloadsDirectory = _tempDir }, NullLogger<RemoteFileService>.Instance);
    }

    [Fact]
    public async Task List_BuildsFindStatCommand_AndParses()
    {
        _adb.ShellResponse = (_, _) => ProcessResult.Ok("directory|4096|1760000000|/sdcard/DCIM/Camera\nregular file|10|1760000000|/sdcard/DCIM/a.jpg\n");

        var entries = await _service.ListAsync(Serial, "/sdcard/DCIM/");

        Assert.Equal($"shell {Serial} find /sdcard/DCIM -maxdepth 1 -mindepth 1 -exec stat -c %F|%s|%Y|%n {{}} +", _adb.Calls.Single());
        Assert.Equal(["Camera", "a.jpg"], entries.Select(e => e.Name));
    }

    [Fact]
    public async Task List_EmptyFolder_ReturnsEmpty()
    {
        _adb.ShellResponse = (_, _) => ProcessResult.Ok("");
        Assert.Empty(await _service.ListAsync(Serial, "/sdcard/Kosong"));
    }

    [Fact]
    public async Task List_PermissionDenied_Throws()
    {
        _adb.ShellResponse = (_, _) => ProcessResult.Fail(1, "", "find: /sdcard/Android/data: Permission denied");

        var ex = await Assert.ThrowsAsync<RemoteFileException>(() => _service.ListAsync(Serial, "/sdcard/Android/data"));
        Assert.Contains("Permission denied", ex.Message);
    }

    [Fact]
    public async Task Pull_OnePerEntry_ReportsProgressAndFailures()
    {
        var entries = new[]
        {
            new RemoteEntry("/sdcard/DCIM/a.jpg", false, 100, DateTimeOffset.UnixEpoch),
            new RemoteEntry("/sdcard/DCIM/b.jpg", false, 300, DateTimeOffset.UnixEpoch),
        };
        _adb.RunResponse = args => args[3].EndsWith("b.jpg") ? ProcessResult.Fail(1, "", "adb: error: failed to stat remote object '/sdcard/DCIM/b.jpg': No such file or directory") : ProcessResult.Ok("1 file pulled, 0 skipped.");
        var reports = new List<TransferProgress>();

        var result = await _service.PullAsync(Serial, entries, _tempDir, new SyncProgress(reports));

        Assert.Equal([$"run -s {Serial} pull /sdcard/DCIM/a.jpg {_tempDir}", $"run -s {Serial} pull /sdcard/DCIM/b.jpg {_tempDir}"], _adb.Calls);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(100, result.Bytes);
        Assert.Single(result.Failures);
        Assert.Equal("b.jpg", result.Failures[0].Name);
        Assert.Contains("No such file", result.Failures[0].Message);
        Assert.True(Directory.Exists(_tempDir));
        Assert.Equal([0, 100, 100], reports.Select(r => r.DoneBytes));
        Assert.Equal(400, reports[0].TotalBytes);
        Assert.Equal("a.jpg", reports[0].CurrentName);
        Assert.Equal(0.25, reports[^1].Fraction, precision: 3); // b.jpg gagal: byte-nya tidak dihitung
        Assert.Equal(2, reports[^1].FileIndex);
    }

    [Fact]
    public async Task Pull_Cancelled_StopsEarly()
    {
        var entries = new[] { new RemoteEntry("/sdcard/a", false, 1, DateTimeOffset.UnixEpoch), new RemoteEntry("/sdcard/b", false, 1, DateTimeOffset.UnixEpoch) };
        using var cts = new CancellationTokenSource();
        _adb.RunResponse = _ => { cts.Cancel(); return ProcessResult.Ok(); };

        var result = await _service.PullAsync(Serial, entries, _tempDir, null, cts.Token);

        Assert.True(result.Cancelled);
        Assert.Equal(1, result.Succeeded);
        Assert.Single(_adb.Calls);
    }

    [Fact]
    public async Task Push_UsesLocalSizes_AndTrailingSlash()
    {
        Directory.CreateDirectory(_tempDir);
        var local = Path.Combine(_tempDir, "kirim.txt");
        await File.WriteAllBytesAsync(local, new byte[123]);
        var reports = new List<TransferProgress>();

        var result = await _service.PushAsync(Serial, [local], "/sdcard/Download", new SyncProgress(reports));

        Assert.Equal($"run -s {Serial} push {local} /sdcard/Download/", _adb.Calls.Single());
        Assert.True(result.AllSucceeded);
        Assert.Equal(123, result.Bytes);
        Assert.Equal(123, reports[0].TotalBytes);
    }

    [Fact]
    public async Task CreateDirectory_Rename_Delete_Commands()
    {
        await _service.CreateDirectoryAsync(Serial, "/sdcard/Download/", "Folder Baru");
        await _service.RenameAsync(Serial, "/sdcard/Download/a.txt", "b.txt");
        await _service.DeleteAsync(Serial, ["/sdcard/Download/b.txt", "/sdcard/Download/Folder Baru"]);

        Assert.Equal(
        [
            $"shell {Serial} mkdir -p /sdcard/Download/Folder Baru",
            $"shell {Serial} mv /sdcard/Download/a.txt /sdcard/Download/b.txt",
            $"shell {Serial} rm -r /sdcard/Download/b.txt /sdcard/Download/Folder Baru",
        ], _adb.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("..")]
    public async Task InvalidNames_Rejected(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDirectoryAsync(Serial, "/sdcard", name));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RenameAsync(Serial, "/sdcard/x", name));
        Assert.Empty(_adb.Calls);
    }

    [Fact]
    public async Task Delete_Empty_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.DeleteAsync(Serial, []));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>IProgress sinkron (Progress&lt;T&gt; bawaan memakai SynchronizationContext dan tidak deterministik di test).</summary>
    private sealed class SyncProgress(List<TransferProgress> sink) : IProgress<TransferProgress>
    {
        public void Report(TransferProgress value) => sink.Add(value);
    }
}
