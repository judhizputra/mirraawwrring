using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core;
using Mirraawwrring.Core.Setup;

namespace Mirraawwrring.Core.Tests.Setup;

public sealed class SetupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mirraawwrring-tests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public SetupServiceTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new AppPaths(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private SetupService NewService(HttpMessageHandler? handler = null) =>
        new(_paths, new FakeProcessRunner(), new HttpClient(handler ?? new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), NullLogger<SetupService>.Instance);

    /// <summary>Zip kecil meniru struktur scrcpy-win64-v4.1.zip.</summary>
    private static byte[] BuildZip(string folder, params (string Name, string Content)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry($"{folder}/{name}");
                using var w = new StreamWriter(entry.Open());
                w.Write(content);
            }
        }

        return ms.ToArray();
    }

    private static string Sha256Hex(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    [Fact]
    public void Manifest_Parses()
    {
        var m = ScrcpyManifest.Parse("""{"version":"4.1","url":"https://example.com/scrcpy-win64-v4.1.zip","sha256":"abc","folderInZip":"scrcpy-win64-v4.1"}""");
        Assert.Equal("4.1", m.Version);
        Assert.Equal("scrcpy-win64-v4.1.zip", m.FileName);
        Assert.Equal("scrcpy-win64-v4.1", m.FolderInZip);
    }

    [Fact]
    public void RepoManifest_IsValidAndPinsV41()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", "scrcpy-manifest.json"));
        var m = ScrcpyManifest.Load(path);
        Assert.Equal("4.1", m.Version);
        Assert.Equal(64, m.Sha256.Length);
        Assert.StartsWith("https://github.com/Genymobile/scrcpy/releases/download/v4.1/", m.Url);
    }

    [Fact]
    public async Task VerifyChecksum_Mismatch_DeletesFileAndThrows()
    {
        var file = Path.Combine(_root, "x.zip");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);

        var ex = await Assert.ThrowsAsync<ChecksumMismatchException>(() => NewService().VerifyChecksumAsync(file, new string('0', 64)));

        Assert.False(File.Exists(file));
        Assert.Equal(Sha256Hex([1, 2, 3]), ex.Actual);
    }

    [Fact]
    public async Task VerifyChecksum_Match_CaseInsensitive()
    {
        var file = Path.Combine(_root, "x.zip");
        await File.WriteAllBytesAsync(file, [1, 2, 3]);
        await NewService().VerifyChecksumAsync(file, Sha256Hex([1, 2, 3]).ToUpperInvariant());
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void ExtractFolder_StripsPrefixAndOverwrites()
    {
        var zip = Path.Combine(_root, "s.zip");
        File.WriteAllBytes(zip, BuildZip("scrcpy-win64-v4.1", ("scrcpy.exe", "exe"), ("adb.exe", "adb"), ("scrcpy-server", "srv"), ("sub/readme.txt", "r")));
        var target = Path.Combine(_root, "scrcpy");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "scrcpy.exe"), "lama");

        SetupService.ExtractFolder(zip, "scrcpy-win64-v4.1", target);

        Assert.Equal("exe", File.ReadAllText(Path.Combine(target, "scrcpy.exe")));
        Assert.True(File.Exists(Path.Combine(target, "scrcpy-server")));
        Assert.True(File.Exists(Path.Combine(target, "sub", "readme.txt")));
        Assert.False(File.Exists(Path.Combine(target, "scrcpy-win64-v4.1")));
    }

    [Fact]
    public void ExtractFolder_WrongFolderName_Throws()
    {
        var zip = Path.Combine(_root, "s.zip");
        File.WriteAllBytes(zip, BuildZip("lain", ("scrcpy.exe", "exe")));
        Assert.Throws<InvalidDataException>(() => SetupService.ExtractFolder(zip, "scrcpy-win64-v4.1", Path.Combine(_root, "out")));
    }

    [Fact]
    public void ExtractFolder_PathTraversal_Throws()
    {
        var zip = Path.Combine(_root, "s.zip");
        File.WriteAllBytes(zip, BuildZip("f", ("../evil.txt", "x")));
        Assert.Throws<InvalidDataException>(() => SetupService.ExtractFolder(zip, "f", Path.Combine(_root, "out")));
    }

    [Fact]
    public void Unblock_RemovesZoneIdentifier()
    {
        var dir = Path.Combine(_root, "unblock");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "scrcpy.exe");
        File.WriteAllText(file, "x");
        File.WriteAllText(file + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        Assert.True(File.Exists(file + ":Zone.Identifier"));

        var removed = SetupService.UnblockDirectory(dir);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(file + ":Zone.Identifier"));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void ValidateFolder_ReportsMissing()
    {
        var dir = Path.Combine(_root, "folder");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "scrcpy.exe"), "x");

        var v = SetupService.ValidateFolder(dir);

        Assert.False(v.IsValid);
        Assert.Equal(["adb.exe", "scrcpy-server"], v.MissingFiles);

        File.WriteAllText(Path.Combine(dir, "adb.exe"), "x");
        File.WriteAllText(Path.Combine(dir, "scrcpy-server"), "x");
        v = SetupService.ValidateFolder(Path.Combine(dir, "scrcpy.exe"));
        Assert.True(v.IsValid);
        Assert.Equal(Path.Combine(dir, "adb.exe"), v.AdbPath);
    }

    [Fact]
    public async Task DownloadAndInstall_EndToEnd_WithProgress()
    {
        var zipBytes = BuildZip("scrcpy-win64-v4.1", ("scrcpy.exe", "exe"), ("adb.exe", "adb"), ("scrcpy-server", "srv"));
        var manifest = new ScrcpyManifest
        {
            Version = "4.1",
            Url = "https://example.com/scrcpy-win64-v4.1.zip",
            Sha256 = Sha256Hex(zipBytes),
            FolderInZip = "scrcpy-win64-v4.1",
        };
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) });
        var service = NewService(handler);
        var stages = new List<SetupStage>();
        var progress = new SyncProgress(p => stages.Add(p.Stage));

        var target = await service.DownloadAndInstallAsync(manifest, progress);

        Assert.Equal(_paths.ScrcpyBundleDirectory, target);
        Assert.True(File.Exists(Path.Combine(target, "scrcpy.exe")));
        Assert.True(SetupService.ValidateFolder(target).IsValid);
        Assert.Contains(SetupStage.Downloading, stages);
        Assert.Equal(SetupStage.Done, stages[^1]);
        Assert.Equal([SetupStage.Verifying, SetupStage.Extracting, SetupStage.Unblocking, SetupStage.Done], stages.Where(s => s != SetupStage.Downloading));
        Assert.Empty(Directory.GetFiles(Path.Combine(_paths.DataDirectory, "download")));
    }

    [Fact]
    public async Task DownloadAndInstall_BadHash_NothingExtracted()
    {
        var zipBytes = BuildZip("scrcpy-win64-v4.1", ("scrcpy.exe", "exe"));
        var manifest = new ScrcpyManifest { Version = "4.1", Url = "https://example.com/s.zip", Sha256 = new string('a', 64), FolderInZip = "scrcpy-win64-v4.1" };
        var service = NewService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) }));

        await Assert.ThrowsAsync<ChecksumMismatchException>(() => service.DownloadAndInstallAsync(manifest, null));

        Assert.False(Directory.Exists(_paths.ScrcpyBundleDirectory));
        Assert.Empty(Directory.GetFiles(Path.Combine(_paths.DataDirectory, "download")));
    }

    [Fact]
    public async Task HasInternet_UsesHead()
    {
        HttpMethod? seen = null;
        var service = NewService(new FakeHandler(r =>
        {
            seen = r.Method;
            return new HttpResponseMessage(HttpStatusCode.Found);
        }));
        var manifest = new ScrcpyManifest { Version = "4.1", Url = "https://example.com/s.zip", Sha256 = "", FolderInZip = "f" };

        Assert.True(await service.HasInternetAsync(manifest));
        Assert.Equal(HttpMethod.Head, seen);

        var offline = NewService(new FakeHandler(_ => throw new HttpRequestException("offline")));
        Assert.False(await offline.HasInternetAsync(manifest));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class SyncProgress(Action<SetupProgress> handler) : IProgress<SetupProgress>
    {
        public void Report(SetupProgress value) => handler(value);
    }
}
