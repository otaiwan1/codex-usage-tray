using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexUsageTray;

public static class TrayIconRenderer
{
    public static Icon Render(int? remainingPercent, UsagePeriod period)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        var textColor = remainingPercent switch
        {
            null => Color.FromArgb(184, 190, 202),
            > 50 => Color.FromArgb(45, 214, 126),
            > 20 => Color.FromArgb(255, 184, 45),
            _ => Color.FromArgb(255, 91, 91)
        };

        var text = remainingPercent?.ToString() ?? "--";
        using var glyphs = CreateMaximizedGlyphs(text, new RectangleF(1.5f, 1.5f, 29f, 29f));
        using var outline = new Pen(Color.FromArgb(230, 10, 12, 15), 1.5f)
        {
            LineJoin = LineJoin.Round
        };
        using var fill = new SolidBrush(textColor);
        graphics.DrawPath(outline, glyphs);
        graphics.FillPath(fill, glyphs);
        DrawPeriodBadge(graphics, period);

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

    private static void DrawPeriodBadge(Graphics graphics, UsagePeriod period)
    {
        var badgeColor = period switch
        {
            UsagePeriod.FiveHours => Color.FromArgb(0, 184, 230),
            UsagePeriod.SevenDays => Color.FromArgb(131, 112, 255),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        var badgeText = period == UsagePeriod.FiveHours ? "5" : "7";
        var badgeBounds = new RectangleF(18f, 18f, 12.5f, 12.5f);
        using var badgeFill = new SolidBrush(badgeColor);
        using var badgeOutline = new Pen(Color.FromArgb(245, 10, 12, 15), 1.25f);
        graphics.FillEllipse(badgeFill, badgeBounds);
        graphics.DrawEllipse(badgeOutline, badgeBounds);

        using var badgeGlyph = CreateMaximizedGlyphs(badgeText, new RectangleF(21.25f, 20f, 6f, 8.5f));
        using var glyphOutline = new Pen(Color.FromArgb(220, 10, 12, 15), 0.9f)
        {
            LineJoin = LineJoin.Round
        };
        using var glyphFill = new SolidBrush(Color.White);
        graphics.DrawPath(glyphOutline, badgeGlyph);
        graphics.FillPath(glyphFill, badgeGlyph);
    }

    private static GraphicsPath CreateMaximizedGlyphs(string text, RectangleF targetBounds)
    {
        const float initialEmSize = 30f;
        var path = new GraphicsPath();
        using var fontFamily = new FontFamily("Segoe UI");
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(text, fontFamily, (int)FontStyle.Bold, initialEmSize, PointF.Empty, format);

        var bounds = path.GetBounds();
        using var scale = new Matrix();
        scale.Scale(targetBounds.Width / bounds.Width, targetBounds.Height / bounds.Height);
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
