using Microsoft.Extensions.Logging.Abstractions;
using Mirraawwrring.Core.Adb;
using Mirraawwrring.Core.Devices;
using Mirraawwrring.Core.Processes;
using Mirraawwrring.Core.Setup;
using Mirraawwrring.Core.Tools;

namespace Mirraawwrring.Core.Tests.Setup;

public sealed class VerificationServiceTests
{
    private const string Dir = @"C:\apps\Mirraawwrring\scrcpy";
    private readonly FakeFileSystem _fs = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeAdbClient _adb = new();
    private readonly ToolRegistry _tools = new();

    public VerificationServiceTests()
    {
        _fs.AddFile(VerificationService.ScrcpyRequiredFiles.Select(f => Path.Combine(Dir, f)).ToArray());
        _tools.Update(new ToolLocation { ScrcpyPath = Path.Combine(Dir, "scrcpy.exe"), AdbPath = Path.Combine(Dir, "adb.exe") });
        _runner.When(["--version"], ProcessResult.Ok("scrcpy 4.1 <https://github.com/Genymobile/scrcpy>\n"));
    }

    private VerificationService NewService() =>
        new(_tools, _fs, _runner, _adb, new InjectionProbe(_adb, NullLogger<InjectionProbe>.Instance), NullLogger<VerificationService>.Instance);

    [Fact]
    public async Task AllGood_WithPhoneAllowed()
    {
        _adb.Enqueue(TestDevices.Usb(model: "2407FPN8EG"));
        var reported = new List<CheckResult>();

        var results = await NewService().RunAsync(skipPhone: false, reported.Add);

        Assert.Equal(VerificationService.AllChecks, results.Select(r => r.Id));
        Assert.All(results, r => Assert.Equal(CheckStatus.Ok, r.Status));
        Assert.Equal("scrcpy 4.1", results[1].Detail);
        Assert.Equal("adb 1.0.41", results[2].Detail);
        Assert.Equal("2407FPN8EG", results[5].Detail);
        Assert.Contains(reported, r => r.Status == CheckStatus.Running);
        Assert.Contains("shell R58N12ABCDE input keyevent 0", _adb.Calls);
    }

    [Fact]
    public async Task SkipPhone_MarksPhoneChecksSkipped()
    {
        var results = await NewService().RunAsync(skipPhone: true, _ => { });

        Assert.Equal(CheckStatus.Skipped, results.Single(r => r.Id == CheckId.PhoneAuthorized).Status);
        Assert.Equal(CheckStatus.Skipped, results.Single(r => r.Id == CheckId.PhoneInjection).Status);
        Assert.DoesNotContain(_adb.Calls, c => c.StartsWith("shell", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingServerFile_FailsWithRedownload()
    {
        var fs = new FakeFileSystem().AddFile(Path.Combine(Dir, "scrcpy.exe"), Path.Combine(Dir, "adb.exe"));
        var service = new VerificationService(_tools, fs, _runner, _adb, new InjectionProbe(_adb, NullLogger<InjectionProbe>.Instance), NullLogger<VerificationService>.Instance);

        var r = service.CheckScrcpyFiles(_tools.Current);

        Assert.Equal(CheckStatus.Failed, r.Status);
        Assert.Equal(FixAction.Redownload, r.Fix);
        Assert.Contains("scrcpy-server", r.Detail);
    }

    [Fact]
    public void MissingOnlyDlls_IsOkWithNote()
    {
        var fs = new FakeFileSystem().AddFile(Path.Combine(Dir, "scrcpy.exe"), Path.Combine(Dir, "adb.exe"), Path.Combine(Dir, "scrcpy-server"));
        var service = new VerificationService(_tools, fs, _runner, _adb, new InjectionProbe(_adb, NullLogger<InjectionProbe>.Instance), NullLogger<VerificationService>.Instance);

        Assert.Equal(CheckStatus.Ok, service.CheckScrcpyFiles(_tools.Current).Status);
    }

    [Fact]
    public async Task ScrcpyCannotRun_Fails()
    {
        var runner = new FakeProcessRunner().When(["--version"], ProcessResult.Fail(-1, "", "Access is denied"));
        var service = new VerificationService(_tools, _fs, runner, _adb, new InjectionProbe(_adb, NullLogger<InjectionProbe>.Instance), NullLogger<VerificationService>.Instance);

        var results = await service.RunAsync(true, _ => { });

        var r = results.Single(x => x.Id == CheckId.ScrcpyRuns);
        Assert.Equal(CheckStatus.Failed, r.Status);
        Assert.Equal(FixAction.Redownload, r.Fix);
    }

    [Fact]
    public async Task AdbServerConflict_OffersKillOtherAdb()
    {
        _tools.Update(_tools.Current with { OtherAdbPaths = [@"C:\Android\platform-tools\adb.exe"] });
        _adb.StartServerResponse = new AdbServerStartResult(false, true, true, "adb server version (41) doesn't match this client (39); killing...\nerror: could not install *smartsocket* listener");

        var results = await NewService().RunAsync(true, _ => { });

        var server = results.Single(r => r.Id == CheckId.AdbServer);
        Assert.Equal(CheckStatus.Failed, server.Status);
        Assert.Equal(FixAction.KillOtherAdb, server.Fix);
        Assert.Equal(@"C:\Android\platform-tools\adb.exe", server.FixArgument);
        Assert.Equal(CheckStatus.Warning, results.Single(r => r.Id == CheckId.OtherAdb).Status);
    }

    [Fact]
    public async Task PhoneUnauthorized_FailsAndSkipsInjection()
    {
        _adb.Enqueue(TestDevices.Usb(state: DeviceState.Unauthorized));

        var results = await NewService().RunAsync(false, _ => { });

        Assert.Equal(CheckStatus.Failed, results.Single(r => r.Id == CheckId.PhoneAuthorized).Status);
        Assert.Equal(FixAction.EnableUsbDebugging, results.Single(r => r.Id == CheckId.PhoneAuthorized).Fix);
        Assert.Equal(CheckStatus.Skipped, results.Single(r => r.Id == CheckId.PhoneInjection).Status);
    }

    [Fact]
    public async Task InjectionBlocked_IsWarningWithSecuritySettings()
    {
        _adb.Enqueue(TestDevices.Usb());
        _adb.ShellResponse = (_, _) => ProcessResult.Fail(255, "", "... INJECT_EVENTS permission.");

        var results = await NewService().RunAsync(false, _ => { });

        var inj = results.Single(r => r.Id == CheckId.PhoneInjection);
        Assert.Equal(CheckStatus.Warning, inj.Status);
        Assert.Equal(FixAction.SecuritySettings, inj.Fix);
    }

    [Fact]
    public async Task KillOtherAdb_RunsKillServerOnThatAdb()
    {
        await NewService().KillOtherAdbAsync(@"C:\Android\platform-tools\adb.exe");

        Assert.Equal(@"C:\Android\platform-tools\adb.exe", _runner.Last.FileName);
        Assert.Equal(["kill-server"], _runner.Last.Arguments);
    }
}
