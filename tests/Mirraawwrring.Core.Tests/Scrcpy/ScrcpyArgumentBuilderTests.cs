using Mirraawwrring.Core.Scrcpy;

namespace Mirraawwrring.Core.Tests.Scrcpy;

public sealed class ScrcpyArgumentBuilderTests
{
    private const string Serial = "adb-FQYDJFRWIZGUZ989-8Usjcv._adb-tls-connect._tcp";
    private readonly FakeAppPaths _paths = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 16, 45, 12, TimeSpan.FromHours(7)));

    private ScrcpyArgumentBuilder Builder => new(_paths, _time);

    [Fact]
    public void Defaults_SerialTitleBitrateFps()
    {
        var plan = Builder.Build(new ScrcpyOptions(), Serial, "2407FPN8EG");

        Assert.Equal(
            [$"--serial={Serial}", "--window-title=Mirraawwrring - 2407FPN8EG", "--video-bit-rate=8M", "--max-fps=60"],
            plan.Arguments);
        Assert.Equal("Mirraawwrring - 2407FPN8EG", plan.WindowTitle);
        Assert.Null(plan.RecordFilePath);
    }

    [Fact]
    public void NoModel_UsesSerialInTitle()
    {
        var plan = Builder.Build(new ScrcpyOptions(), "R58N12ABCDE", null);
        Assert.Contains("--window-title=Mirraawwrring - R58N12ABCDE", plan.Arguments);
    }

    [Fact]
    public void ViewOnly_AddsNoControl_AndIgnoresUhid()
    {
        var plan = Builder.Build(new ScrcpyOptions { Mode = MirrorMode.ViewOnly, UhidInput = true }, Serial, "M");

        Assert.Contains("--no-control", plan.Arguments);
        Assert.DoesNotContain("--keyboard=uhid", plan.Arguments);
        Assert.DoesNotContain("--mouse=uhid", plan.Arguments);
    }

    [Fact]
    public void ScreenOff_AddsTurnScreenOffAndStayAwake_Once()
    {
        var plan = Builder.Build(new ScrcpyOptions { Mode = MirrorMode.ScreenOff, StayAwake = true }, Serial, "M");

        Assert.Contains("--turn-screen-off", plan.Arguments);
        Assert.Equal(1, plan.Arguments.Count(a => a == "--stay-awake"));
    }

    [Fact]
    public void DataSaver_OverridesManualQuality()
    {
        var options = new ScrcpyOptions { Mode = MirrorMode.DataSaver, MaxSize = 1920, VideoBitRateMbps = 16, MaxFps = null };

        var plan = Builder.Build(options, Serial, "M");

        Assert.Contains("--max-size=1024", plan.Arguments);
        Assert.Contains("--video-bit-rate=4M", plan.Arguments);
        Assert.Contains("--max-fps=30", plan.Arguments);
        Assert.DoesNotContain("--max-size=1920", plan.Arguments);
        Assert.DoesNotContain("--video-bit-rate=16M", plan.Arguments);
    }

    [Fact]
    public void Quality_NativeResolutionAndUnlimitedFps_OmitFlags()
    {
        var plan = Builder.Build(new ScrcpyOptions { MaxSize = null, MaxFps = null, VideoBitRateMbps = 2 }, Serial, "M");

        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("--max-size", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Arguments, a => a.StartsWith("--max-fps", StringComparison.Ordinal));
        Assert.Contains("--video-bit-rate=2M", plan.Arguments);
    }

    [Fact]
    public void Quality_ManualValues()
    {
        var plan = Builder.Build(new ScrcpyOptions { MaxSize = 1280, MaxFps = 30, VideoBitRateMbps = 4 }, Serial, "M");

        Assert.Contains("--max-size=1280", plan.Arguments);
        Assert.Contains("--max-fps=30", plan.Arguments);
        Assert.Contains("--video-bit-rate=4M", plan.Arguments);
    }

    [Fact]
    public void AllOptions_On()
    {
        var options = new ScrcpyOptions
        {
            ForwardAudio = false,
            UhidInput = true,
            AlwaysOnTop = true,
            Fullscreen = true,
            StayAwake = true,
            RecordToFile = true,
        };

        var plan = Builder.Build(options, Serial, "M");

        Assert.Contains("--no-audio", plan.Arguments);
        Assert.Contains("--keyboard=uhid", plan.Arguments);
        Assert.Contains("--mouse=uhid", plan.Arguments);
        Assert.Contains("--always-on-top", plan.Arguments);
        Assert.Contains("--fullscreen", plan.Arguments);
        Assert.Contains("--stay-awake", plan.Arguments);
        var expectedPath = Path.Combine(_paths.RecordingsDirectory, "scrcpy_20261014_164512.mp4");
        Assert.Equal(expectedPath, plan.RecordFilePath);
        Assert.Contains($"--record={expectedPath}", plan.Arguments);
    }

    [Fact]
    public void ForwardAudio_Default_NoFlag()
    {
        var plan = Builder.Build(new ScrcpyOptions(), Serial, "M");
        Assert.DoesNotContain("--no-audio", plan.Arguments);
    }

    [Fact]
    public void HeadlessRecording_NoWindowNoPlaybackNoControl()
    {
        var plan = Builder.BuildHeadlessRecording("SER", "M", withAudio: false);

        Assert.Equal("--serial=SER", plan.Arguments[0]);
        Assert.Contains("--no-window", plan.Arguments);
        Assert.Contains("--no-playback", plan.Arguments);
        Assert.Contains("--no-control", plan.Arguments);
        Assert.Contains("--no-audio", plan.Arguments);
        Assert.NotNull(plan.RecordFilePath);
        Assert.EndsWith(".mp4", plan.RecordFilePath);
    }

    [Fact]
    public void EmptySerial_Throws()
    {
        Assert.Throws<ArgumentException>(() => Builder.Build(new ScrcpyOptions(), " ", "M"));
    }

    [Fact]
    public void ToCommandLine_QuotesTitleWithSpaces()
    {
        var plan = Builder.Build(new ScrcpyOptions(), "SER", "Xiaomi 2407FPN8EG");
        Assert.Contains("\"--window-title=Mirraawwrring - Xiaomi 2407FPN8EG\"", plan.ToCommandLine());
    }
}
