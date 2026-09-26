using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexUsageTray;

public static class TrayIconRenderer
{
    private const string CondensedNumberFont = "Bahnschrift SemiBold Condensed";
    private const string SmallNumberFont = "Bahnschrift Condensed";

    public static Icon Render(
        int? remainingPercent,
        UsagePeriod period,
        bool showPeriodRibbon = true,
        bool lightTheme = false,
        int size = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 16);
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        var textColor = GetNumberColor(remainingPercent, lightTheme);
        var smallIcon = size <= 20;

        var text = remainingPercent?.ToString() ?? "--";
        if (text.Length <= 2 && remainingPercent is not null)
        {
            using var number = RenderHintedNumber(text, textColor, size, showPeriodRibbon);
            graphics.DrawImageUnscaled(number, 0, 0);
        }
        else
        {
            graphics.ScaleTransform(size / 32f, size / 32f);
            var numberBounds = showPeriodRibbon
                ? new RectangleF(0.5f, 0.5f, 31f, 22.5f)
                : new RectangleF(1.5f, 1.5f, 29f, 29f);
            using var numberFont = CreateNumberFont(smallIcon);
            var numberStyle = smallIcon || numberFont.Name != CondensedNumberFont ? FontStyle.Bold : FontStyle.Regular;
            using var glyphs = CreateFittedGlyphs(text, numberBounds, numberFont, numberStyle);
            using var fill = new SolidBrush(textColor);
            graphics.FillPath(fill, glyphs);
            graphics.ResetTransform();
        }

        if (showPeriodRibbon)
        {
            graphics.ScaleTransform(size / 32f, size / 32f);
            DrawPeriodRibbon(graphics, period, lightTheme, smallIcon);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Color GetNumberColor(int? remainingPercent, bool lightTheme) =>
        lightTheme
            ? remainingPercent switch
            {
                null => Color.FromArgb(57, 67, 79),
                > 50 => Color.FromArgb(31, 38, 45),
                > 20 => Color.FromArgb(143, 82, 0),
                _ => Color.FromArgb(180, 44, 51)
            }
            : remainingPercent switch
            {
                null => Color.FromArgb(212, 218, 228),
                > 50 => Color.FromArgb(241, 245, 249),
                > 20 => Color.FromArgb(255, 208, 90),
                _ => Color.FromArgb(255, 136, 136)
            };

    private static Bitmap RenderHintedNumber(string text, Color color, int size, bool hasRibbon)
    {
        using var mask = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var maskGraphics = Graphics.FromImage(mask))
        {
            maskGraphics.Clear(Color.White);
            var fontSize = (hasRibbon ? 13f : 15f) * size / 16f;
            using var font = new Font("Microsoft Sans Serif", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            var bounds = new Rectangle(0, 0, size, hasRibbon ? size - 2 : size);
            var flags = TextFormatFlags.NoPadding |
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(maskGraphics, text, font, bounds, Color.Black, Color.White, flags);
        }

        var number = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var pixel = mask.GetPixel(x, y);
                var coverage = 255 - (int)Math.Round(
                    0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B);
                if (coverage > 0)
                {
                    number.SetPixel(x, y, Color.FromArgb(coverage, color));
                }
            }
        }

        return number;
    }

    private static void DrawPeriodRibbon(Graphics graphics, UsagePeriod period, bool lightTheme, bool smallIcon)
    {
        var ribbonColor = (period, lightTheme) switch
        {
            (UsagePeriod.FiveHours, true) => Color.FromArgb(0, 107, 139),
            (UsagePeriod.SevenDays, true) => Color.FromArgb(105, 68, 183),
            (UsagePeriod.FiveHours, false) => Color.FromArgb(41, 202, 232),
            (UsagePeriod.SevenDays, false) => Color.FromArgb(165, 138, 255),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        using var ribbonFill = new SolidBrush(ribbonColor);
        if (smallIcon)
        {
            graphics.FillRectangle(ribbonFill, 1f, 28f, 30f, 4f);
            return;
        }

        graphics.FillRectangle(ribbonFill, 1f, 25f, 30f, 7f);

        var label = period == UsagePeriod.FiveHours ? "5h" : "7d";
        using var labelFont = new FontFamily("Segoe UI");
        using var labelGlyphs = CreateFittedGlyphs(label, new RectangleF(10f, 25f, 12f, 6.2f), labelFont, FontStyle.Bold);
        using var labelFill = new SolidBrush(lightTheme ? Color.FromArgb(248, 250, 253) : Color.FromArgb(17, 22, 29));
        graphics.FillPath(labelFill, labelGlyphs);
    }

    private static FontFamily CreateNumberFont(bool smallIcon)
    {
        try
        {
            return new FontFamily(smallIcon ? SmallNumberFont : CondensedNumberFont);
        }
        catch (ArgumentException)
        {
            return new FontFamily("Segoe UI");
        }
    }

    private static GraphicsPath CreateFittedGlyphs(string text, RectangleF targetBounds, FontFamily fontFamily, FontStyle style)
    {
        const float initialEmSize = 30f;
        var path = new GraphicsPath();
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(text, fontFamily, (int)style, initialEmSize, PointF.Empty, format);

        var bounds = path.GetBounds();
        using var scale = new Matrix();
        var factor = Math.Min(targetBounds.Width / bounds.Width, targetBounds.Height / bounds.Height);
        scale.Scale(factor, factor);
        path.Transform(scale);

        bounds = path.GetBounds();
        using var center = new Matrix();
        center.Translate(
            targetBounds.Left + ((targetBounds.Width - bounds.Width) / 2f) - bounds.Left,
            targetBounds.Top + ((targetBounds.Height - bounds.Height) / 2f) - bounds.Top);
        path.Transform(center);
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
