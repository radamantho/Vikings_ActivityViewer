param([string]$OutIco, [string]$OutPreview)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class LogIcon
{
    static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Bitmap Master()
    {
        var bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using (var bg = Round(8, 8, 240, 240, 52))
            using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(256, 256), Color.FromArgb(59, 130, 246), Color.FromArgb(30, 58, 138)))
                g.FillPath(brush, bg);

            using (var shadow = Round(62, 40, 124, 168, 16))
            using (var sb = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            {
                g.TranslateTransform(4, 6);
                g.FillPath(sb, shadow);
                g.ResetTransform();
            }

            using (var page = Round(58, 34, 124, 168, 16))
            using (var pb = new SolidBrush(Color.FromArgb(248, 250, 252)))
                g.FillPath(pb, page);

            using (var accent = new Pen(Color.FromArgb(59, 130, 246), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(accent, 82, 68, 126, 68);
            using (var line = new Pen(Color.FromArgb(148, 163, 184), 10) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(line, 82, 98, 158, 98);
                g.DrawLine(line, 82, 124, 150, 124);
                g.DrawLine(line, 82, 150, 132, 150);
            }

            var gold = Color.FromArgb(250, 204, 21);
            using (var handle = new Pen(Color.FromArgb(120, 53, 15), 24) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(handle, 192, 192, 222, 222);
            using (var handleTop = new Pen(gold, 16) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(handleTop, 192, 192, 220, 220);
            using (var lens = new SolidBrush(Color.FromArgb(150, 191, 219, 254)))
                g.FillEllipse(lens, 128, 128, 72, 72);
            using (var ring = new Pen(gold, 13))
                g.DrawEllipse(ring, 128, 128, 72, 72);
            using (var shine = new Pen(Color.FromArgb(200, 255, 255, 255), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(shine, 142, 142, 44, 44, 200, 70);
        }
        return bmp;
    }

    static byte[] Png(Bitmap master, int size)
    {
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(master, 0, 0, size, size);
            }
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public static void Write(string icoPath, string previewPath)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        using (var master = Master())
        {
            master.Save(previewPath, ImageFormat.Png);
            var images = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) images[i] = Png(master, sizes[i]);

            using (var fs = File.Create(icoPath))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((short)1);
                    w.Write((short)32);
                    w.Write(images[i].Length);
                    w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var image in images) w.Write(image);
            }
        }
    }
}
"@

[LogIcon]::Write($OutIco, $OutPreview)
Get-Item $OutIco | Select-Object Name, Length
