using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SerialHidWrapper.Ui;

internal static class BarcodeIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var background = new SolidBrush(Color.White);
            using var outline = new Pen(Color.FromArgb(31, 41, 55), 2);
            graphics.FillRoundedRectangle(background, new Rectangle(2, 3, 28, 26), 4);
            graphics.DrawRoundedRectangle(outline, new Rectangle(2, 3, 28, 26), 4);

            graphics.SmoothingMode = SmoothingMode.None;
            using var bars = new SolidBrush(Color.FromArgb(17, 24, 39));
            foreach (var rectangle in new[]
            {
                new Rectangle(7, 8, 2, 15),
                new Rectangle(11, 8, 1, 15),
                new Rectangle(14, 8, 3, 15),
                new Rectangle(19, 8, 1, 15),
                new Rectangle(22, 8, 3, 15)
            })
            {
                graphics.FillRectangle(bars, rectangle);
            }
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
