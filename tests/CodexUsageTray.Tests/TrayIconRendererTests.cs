using System.Drawing;

namespace CodexUsageTray.Tests;

public sealed class TrayIconRendererTests
{
    [Fact]
    public void PercentageGlyphUsesTransparentBackgroundAndAvailableCanvas()
    {
        using var icon = TrayIconRenderer.Render(98, UsagePeriod.SevenDays);
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

    [Fact]
    public void PeriodRibbonDistinguishesFiveHoursFromSevenDaysWithoutChangingNumber()
    {
        using var fiveHourIcon = TrayIconRenderer.Render(98, UsagePeriod.FiveHours);
        using var sevenDayIcon = TrayIconRenderer.Render(98, UsagePeriod.SevenDays);
        using var fiveHourBitmap = fiveHourIcon.ToBitmap();
        using var sevenDayBitmap = sevenDayIcon.ToBitmap();

        for (var y = 0; y < 23; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.Equal(fiveHourBitmap.GetPixel(x, y), sevenDayBitmap.GetPixel(x, y));
            }
        }

        var differentRibbonPixels = 0;
        for (var y = 25; y < 32; y++)
        {
            for (var x = 1; x < 31; x++)
            {
                if (fiveHourBitmap.GetPixel(x, y) != sevenDayBitmap.GetPixel(x, y))
                {
                    differentRibbonPixels++;
                }
            }
        }

        Assert.True(differentRibbonPixels > 100, $"Only {differentRibbonPixels} ribbon pixels differed.");
    }

    [Fact]
    public void SingleWindowIconHasNoPeriodRibbon()
    {
        using var plainIcon = TrayIconRenderer.Render(98, UsagePeriod.SevenDays, showPeriodRibbon: false);
        using var ribbonIcon = TrayIconRenderer.Render(98, UsagePeriod.SevenDays);
        using var plain = plainIcon.ToBitmap();
        using var ribbon = ribbonIcon.ToBitmap();

        Assert.Equal(0, plain.GetPixel(16, 31).A);
        Assert.True(ribbon.GetPixel(16, 31).A > 16);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void IconKeepsNumberAndPeriodColorVisibleAtDifferentScaling(int size)
    {
        using var icon = TrayIconRenderer.Render(68, UsagePeriod.FiveHours, lightTheme: true, size: size);
        using var bitmap = icon.ToBitmap();

        Assert.Equal(new Size(size, size), bitmap.Size);
        var numberPixels = 0;
        for (var y = 0; y < size * 3 / 4; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (bitmap.GetPixel(x, y).A > 128)
                {
                    numberPixels++;
                }
            }
        }

        Assert.True(numberPixels >= 20, $"Only {numberPixels} number pixels were visible.");
        Assert.True(bitmap.GetPixel(size / 2, size - 1).A > 128);
    }

    [Fact]
    public void LightThemeUsesDarkerNumberColor()
    {
        using var darkIcon = TrayIconRenderer.Render(68, UsagePeriod.FiveHours, lightTheme: false, size: 16);
        using var lightIcon = TrayIconRenderer.Render(68, UsagePeriod.FiveHours, lightTheme: true, size: 16);
        using var dark = darkIcon.ToBitmap();
        using var light = lightIcon.ToBitmap();

        var foundOpaqueNumberPixel = false;
        for (var y = 0; y < 13 && !foundOpaqueNumberPixel; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var darkPixel = dark.GetPixel(x, y);
                var lightPixel = light.GetPixel(x, y);
                if (darkPixel.A < 240 || lightPixel.A < 240)
                {
                    continue;
                }

                Assert.True(
                    lightPixel.R + lightPixel.G + lightPixel.B < darkPixel.R + darkPixel.G + darkPixel.B);
                foundOpaqueNumberPixel = true;
                break;
            }
        }

        Assert.True(foundOpaqueNumberPixel, "No opaque number pixel was found in both themes.");
    }
}
