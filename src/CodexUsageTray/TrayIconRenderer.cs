using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexUsageTray;

public static class TrayIconRenderer
{
    public static Icon Render(int? remainingPercent)
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
        using var glyphs = CreateMaximizedGlyphs(text, size);
        using var outline = new Pen(Color.FromArgb(230, 10, 12, 15), 1.5f)
        {
            LineJoin = LineJoin.Round
        };
        using var fill = new SolidBrush(textColor);
        graphics.DrawPath(outline, glyphs);
        graphics.FillPath(fill, glyphs);

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

    private static GraphicsPath CreateMaximizedGlyphs(string text, int iconSize)
    {
        const float initialEmSize = 30f;
        const float availableSize = 29f;
        var path = new GraphicsPath();
        using var fontFamily = new FontFamily("Segoe UI");
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(text, fontFamily, (int)FontStyle.Bold, initialEmSize, PointF.Empty, format);

        var bounds = path.GetBounds();
        var horizontalScale = availableSize / bounds.Width;
        var verticalScale = availableSize / bounds.Height;
        using var transform = new Matrix();
        transform.Scale(horizontalScale, verticalScale);
        path.Transform(transform);

        bounds = path.GetBounds();
        using var center = new Matrix();
        center.Translate(
            ((iconSize - bounds.Width) / 2f) - bounds.Left,
            ((iconSize - bounds.Height) / 2f) - bounds.Top);
        path.Transform(center);
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
