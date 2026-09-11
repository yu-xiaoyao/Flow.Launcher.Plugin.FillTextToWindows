# 生成插件图标 Images/FillTextToWindows.png
# 用法：pwsh -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "..\Flow.Launcher.Plugin.FillTextToWindows\Images"
$outDir = [System.IO.Path]::GetFullPath($outDir)

$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    public static void Save(string path, int size)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        float scale = size / 64f;

        // 圆角背景
        using (var background = RoundedRect(new RectangleF(2 * scale, 2 * scale, (size - 4) * scale, (size - 4) * scale), 15 * scale))
        using (var brush = new LinearGradientBrush(
                   new Point(0, 0),
                   new Point(size, size),
                   Color.FromArgb(255, 79, 140, 255),
                   Color.FromArgb(255, 47, 84, 214)))
        {
            graphics.FillPath(brush, background);
        }

        using var white = new SolidBrush(Color.FromArgb(240, 255, 255, 255));

        // 三个「输入框」，宽度依次变窄
        int[] widths = { 30, 24, 18 };
        int[] tops = { 16, 29, 42 };

        for (int i = 0; i < widths.Length; i++)
        {
            var bar = new RectangleF(13 * scale, tops[i] * scale, widths[i] * scale, 6 * scale);
            using var barPath = RoundedRect(bar, bar.Height / 2);
            graphics.FillPath(white, barPath);
        }

        // 右侧箭头，表示「跳到下一个输入框」
        var arrow = new[]
        {
            new PointF(45 * scale, 25 * scale),
            new PointF(56 * scale, 32 * scale),
            new PointF(45 * scale, 39 * scale),
        };
        graphics.FillPolygon(white, arrow);

        bitmap.Save(path, ImageFormat.Png);
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2;

        if (diameter <= 0 || diameter > Math.Min(rect.Width, rect.Height))
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
'@

$refs = @(
    (Join-Path $PSHOME "System.Drawing.Common.dll"),
    (Join-Path $PSHOME "System.Drawing.Primitives.dll"),
    (Join-Path $PSHOME "System.Private.Windows.Core.dll")
)
Add-Type -TypeDefinition $source -ReferencedAssemblies $refs

$outFile = Join-Path $outDir "FillTextToWindows.png"
[IconMaker]::Save($outFile, 64)

Write-Host "saved: $outFile"
