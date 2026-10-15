using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Setup;

namespace Mirraawwrring.Core.Tests.Setup;

public sealed class UpdateCheckerTests
{
    // Potongan nyata doc/windows.md pada tag v4.1 (format yang sama sejak v2.x).
    private const string WindowsDoc =
        "# On Windows\n\n## Install\n\n### Download\n\nDownload the [latest release]:\n\n" +
        " - [`scrcpy-win64-v4.1.zip`][direct-win64] (64-bit)  \n" +
        "   <sub>SHA-256: `5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db`</sub>\n" +
        " - [`scrcpy-win32-v4.1.zip`][direct-win32] (32-bit)  \n" +
        "   <sub>SHA-256: `fa57b36622a53b6aec74c5e5b5c08236165efa445c4f186d48f176ebf9c24eec`</sub>\n\n" +
        "[direct-win64]: https://github.com/Genymobile/scrcpy/releases/download/v4.1/scrcpy-win64-v4.1.zip\n";

    private const string ReleaseJson = """
        {
          "tag_name": "v5.0.1",
          "html_url": "https://github.com/Genymobile/scrcpy/releases/tag/v5.0.1",
          "assets": [
            { "name": "scrcpy-win32-v5.0.1.zip", "browser_download_url": "https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/scrcpy-win32-v5.0.1.zip" },
            { "name": "scrcpy-win64-v5.0.1.zip", "browser_download_url": "https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/scrcpy-win64-v5.0.1.zip" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://github.com/Genymobile/scrcpy/releases/download/v5.0.1/SHA256SUMS.txt" }
          ]
        }
        """;

    [Fact]
    public void ParseLatestRelease_PicksWin64Asset()
    {
        var release = UpdateChecker.ParseLatestRelease(ReleaseJson)!;

        Assert.Equal("v5.0.1", release.TagName);
        Assert.Equal("5.0.1", release.Version);
        Assert.Equal("https://github.com/Genymobile/scrcpy/releases/tag/v5.0.1", release.HtmlUrl);
        Assert.Equal("scrcpy-win64-v5.0.1.zip", release.Win64ZipName);
        Assert.EndsWith("/v5.0.1/scrcpy-win64-v5.0.1.zip", release.Win64ZipUrl);
    }

    [Fact]
    public void ParseLatestRelease_NoTag_Null()
    {
        Assert.Null(UpdateChecker.ParseLatestRelease("""{"message":"Not Found"}"""));
    }

    [Fact]
    public void ParseWindowsSha256_FindsHashForNamedAsset()
    {
        Assert.Equal("5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db", UpdateChecker.ParseWindowsSha256(WindowsDoc, "scrcpy-win64-v4.1.zip"));
        Assert.Equal("fa57b36622a53b6aec74c5e5b5c08236165efa445c4f186d48f176ebf9c24eec", UpdateChecker.ParseWindowsSha256(WindowsDoc, "scrcpy-win32-v4.1.zip"));
        Assert.Null(UpdateChecker.ParseWindowsSha256(WindowsDoc, "scrcpy-win64-v9.9.zip"));
    }

    [Theory]
    [InlineData("4.1", "4.1", 0)]
    [InlineData("v4.1", "4.1", 0)]
    [InlineData("4.1.1", "4.1", 1)]
    [InlineData("5.0", "4.1", 1)]
    [InlineData("4.1", "5.0.1", -1)]
    [InlineData("scrcpy 4.1 <https://github.com/Genymobile/scrcpy>", "4.1", 0)]
    public void CompareVersions(string a, string b, int expected)
    {
        Assert.Equal(expected, Math.Sign(UpdateChecker.CompareVersions(a, b)));
    }

    [Fact]
    public async Task Check_NewerWithHash_ReturnsVerifiedManifest()
    {
        var checker = NewChecker(req => req.RequestUri!.ToString() switch
        {
            UpdateChecker.LatestReleaseApi => Json(ReleaseJson),
            "https://raw.githubusercontent.com/Genymobile/scrcpy/v5.0.1/doc/windows.md" => Text(WindowsDoc.Replace("v4.1", "v5.0.1")),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });

        var result = await checker.CheckAsync("4.1");

        Assert.NotNull(result);
        Assert.True(result.IsNewer);
        Assert.True(result.CanInstall);
        Assert.Equal("5.0.1", result.VerifiedManifest!.Version);
        Assert.Equal("5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db", result.VerifiedManifest.Sha256);
        Assert.Equal("scrcpy-win64-v5.0.1", result.VerifiedManifest.FolderInZip);
        Assert.EndsWith("scrcpy-win64-v5.0.1.zip", result.VerifiedManifest.Url);
    }

    [Fact]
    public async Task Check_NewerWithoutHash_NoManifest()
    {
        var checker = NewChecker(req => req.RequestUri!.ToString() == UpdateChecker.LatestReleaseApi ? Json(ReleaseJson) : Text("# On Windows\n\nformat baru tanpa hash\n"));

        var result = await checker.CheckAsync("4.1");

        Assert.NotNull(result);
        Assert.True(result.IsNewer);
        Assert.False(result.CanInstall);
        Assert.Null(result.VerifiedManifest);
        Assert.Equal("https://github.com/Genymobile/scrcpy/releases/tag/v5.0.1", result.Latest.HtmlUrl);
    }

    [Fact]
    public async Task Check_AlreadyLatest_NotNewer()
    {
        var checker = NewChecker(_ => Json(ReleaseJson));
        var result = await checker.CheckAsync("5.0.1");
        Assert.NotNull(result);
        Assert.False(result.IsNewer);
    }

    [Fact]
    public async Task Check_HttpFailure_Null()
    {
        var checker = NewChecker(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        Assert.Null(await checker.CheckAsync("4.1"));

        var throwing = NewChecker(_ => throw new HttpRequestException("offline"));
        Assert.Null(await throwing.CheckAsync("4.1"));
    }

    private static UpdateChecker NewChecker(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHandler(respond)), NullLogger<UpdateChecker>.Instance);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Text(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
