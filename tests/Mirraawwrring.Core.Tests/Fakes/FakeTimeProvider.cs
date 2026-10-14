namespace Mirraawwrring.Core.Tests;

/// <summary>TimeProvider dengan waktu tetap yang bisa dimajukan manual.</summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("fake", now.Offset, "fake", "fake");

    public void Advance(TimeSpan by) => _now += by;
}
