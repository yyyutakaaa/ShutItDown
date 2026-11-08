# Draws the app icon. Run from the repo root:
#   powershell -ExecutionPolicy Bypass -File tools\generate-logo.ps1
param(
    [string]$AssetsDir = "src\ShutItDown.Tray\Assets",
    [string]$WebDir = "src\ShutItDown.Web\wwwroot"
)
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class Logo
{
    static readonly Color Tile = Color.FromArgb(0xFF, 0x5A, 0x1F);   // signal orange
    static readonly Color Mark = Color.FromArgb(0x14, 0x14, 0x14);   // near black

    public static byte[] Render(int size, bool rounded = true)
    {
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float s = size / 256f;
            // Small icons get relatively thicker strokes so they stay readable.
            float weight = size <= 24 ? 1.35f : size <= 48 ? 1.15f : 1f;

            // Tile: rounded square. Phones round home screen icons themselves, so those stay square.
            float r = rounded ? 52 * s : 0.01f;
            using (var path = new GraphicsPath())
            {
                float w = size;
                path.AddArc(0, 0, 2 * r, 2 * r, 180, 90);
                path.AddArc(w - 2 * r, 0, 2 * r, 2 * r, 270, 90);
                path.AddArc(w - 2 * r, w - 2 * r, 2 * r, 2 * r, 0, 90);
                path.AddArc(0, w - 2 * r, 2 * r, 2 * r, 90, 90);
                path.CloseFigure();
                using (var brush = new SolidBrush(Tile)) g.FillPath(brush, path);
            }

            // Power symbol: open ring + stem.
            float stroke = 24 * s * weight;
            float cx = 128 * s, cy = 138 * s, radius = 64 * s;
            using (var pen = new Pen(Mark, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                // GDI+ angles: 0 = east, clockwise. Gap of 80 degrees centred on north (270).
                g.DrawArc(pen, cx - radius, cy - radius, 2 * radius, 2 * radius, 310, 280);
                g.DrawLine(pen, cx, 52 * s, cx, 128 * s);
            }

            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public static void WriteIco(string path, int[] sizes)
    {
        var images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) images[i] = Render(sizes[i]);

        using (var fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(images[i].Length); w.Write(offset);
                offset += images[i].Length;
            }
            foreach (var img in images) w.Write(img);
        }
    }
}
'@
[Logo]::WriteIco((Join-Path $AssetsDir 'ShutdownServer.ico'), @(16, 20, 24, 32, 40, 48, 64, 128, 256))
[IO.File]::WriteAllBytes((Join-Path $AssetsDir 'logo.png'), [Logo]::Render(512))
[IO.File]::WriteAllBytes((Join-Path $WebDir 'icon-192.png'), [Logo]::Render(192))
[IO.File]::WriteAllBytes((Join-Path $WebDir 'apple-touch-icon.png'), [Logo]::Render(180, $false))
