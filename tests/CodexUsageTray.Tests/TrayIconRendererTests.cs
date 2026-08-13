using System.Drawing;

namespace CodexUsageTray.Tests;

public sealed class TrayIconRendererTests
{
    [Fact]
    public void PercentageGlyphUsesTransparentBackgroundAndAvailableCanvas()
    {
        using var icon = TrayIconRenderer.Render(98);
        using var bitmap = icon.ToBitmap();

        var visible = new List<Point>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 16)
                {
                    visible.Add(new Point(x, y));
                }
            }
        }

        Assert.NotEmpty(visible);
        var visibleWidth = visible.Max(point => point.X) - visible.Min(point => point.X);
        var visibleHeight = visible.Max(point => point.Y) - visible.Min(point => point.Y);
        Assert.True(visibleWidth >= 26, $"Visible glyph width was {visibleWidth}px.");
        Assert.True(visibleHeight >= 24, $"Visible glyph height was {visibleHeight}px.");
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        Assert.Equal(0, bitmap.GetPixel(31, 31).A);
    }
}
