using Mirraawwrring.Core.Interop;

namespace Mirraawwrring.Core.Tests.Interop;

public sealed class DockPlacementTests
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Right_WhenRoomAvailable()
    {
        var frame = new PixelRect(100, 50, 500, 900);

        var (rect, side) = DockPlacement.Compute(frame, clientTop: 80, WorkArea, width: 256, height: 820, gap: 6);

        Assert.Equal(DockSide.Right, side);
        Assert.Equal(506, rect.Left);
        Assert.Equal(80, rect.Top);
        Assert.Equal(256, rect.Width);
        Assert.Equal(820, rect.Height);
    }

    [Fact]
    public void Left_WhenNoRoomOnRight()
    {
        var frame = new PixelRect(1500, 50, 1900, 900);

        var (rect, side) = DockPlacement.Compute(frame, 80, WorkArea, 256, 820, 6);

        Assert.Equal(DockSide.Left, side);
        Assert.Equal(1500 - 6 - 256, rect.Left);
    }

    [Fact]
    public void Right_ClampedWhenNoRoomEitherSide()
    {
        var frame = new PixelRect(0, 0, 1920, 1040);

        var (rect, side) = DockPlacement.Compute(frame, 30, WorkArea, 256, 820, 6);

        Assert.Equal(DockSide.Right, side);
        Assert.Equal(1920 - 256, rect.Left);
    }

    [Fact]
    public void Top_ClampedToWorkArea()
    {
        var frame = new PixelRect(100, 600, 500, 1400);

        var (rect, _) = DockPlacement.Compute(frame, 630, WorkArea, 256, 820, 6);

        Assert.Equal(1040 - 820, rect.Top);
        Assert.Equal(1040, rect.Bottom);
    }

    [Fact]
    public void Top_NeverAboveWorkArea()
    {
        var frame = new PixelRect(100, -40, 500, 300);

        var (rect, _) = DockPlacement.Compute(frame, -10, WorkArea, 256, 300, 6);

        Assert.Equal(0, rect.Top);
    }

    [Fact]
    public void SecondMonitor_UsesItsWorkArea()
    {
        var work = new PixelRect(1920, 0, 4480, 1400);
        var frame = new PixelRect(4100, 100, 4400, 1000);

        var (rect, side) = DockPlacement.Compute(frame, 130, work, 384, 1230, 9);

        Assert.Equal(DockSide.Left, side);
        Assert.Equal(4100 - 9 - 384, rect.Left);
        Assert.Equal(130, rect.Top);
    }

    [Theory]
    [InlineData(256, 96u, 256)]
    [InlineData(256, 144u, 384)]
    [InlineData(6, 120u, 8)]
    public void DipToPixels_RoundsByDpi(double dip, uint dpi, int expected)
    {
        Assert.Equal(expected, DockPlacement.DipToPixels(dip, dpi));
        Assert.Equal(dip, DockPlacement.PixelsToDip(expected, dpi), precision: 0);
    }
}
