param(
    [Parameter(Mandatory)][string]$PlanFile,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$imageReferences = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Color].Assembly.Location)
$imageReferences += [System.Drawing.Bitmap].Assembly.GetReferencedAssemblies() |
    Where-Object { $_.Name -like 'System.Private.Windows.*' } |
    ForEach-Object { [System.Reflection.Assembly]::Load($_).Location }
Add-Type -ReferencedAssemblies $imageReferences -TypeDefinition @'
using System;
using System.Drawing;
public static class ImageRegionBlend
{
    public static void Outline(Bitmap target, int radius)
    {
        using (var source = (Bitmap)target.Clone())
        for (int y = 0; y < target.Height; y++)
        for (int x = 0; x < target.Width; x++)
        {
            Color pixel = source.GetPixel(x, y);
            if (pixel.A == 255) continue;
            int edge = 0;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int sx = x + dx, sy = y + dy;
                if (sx >= 0 && sy >= 0 && sx < source.Width && sy < source.Height)
                    edge = Math.Max(edge, source.GetPixel(sx, sy).A);
            }
            double front = pixel.A / 255.0, back = edge / 255.0 * (1 - front), alpha = front + back;
            if (alpha == 0) continue;
            target.SetPixel(x, y, Color.FromArgb((int)Math.Round(alpha * 255),
                (int)Math.Round((pixel.R * front + 255 * back) / alpha),
                (int)Math.Round((pixel.G * front + 255 * back) / alpha),
                (int)Math.Round((pixel.B * front + 255 * back) / alpha)));
        }
    }
    public static void Feather(Bitmap original, Bitmap target, Rectangle region, int width)
    {
        for (int y = region.Top; y < region.Bottom; y++)
        for (int x = region.Left; x < region.Right; x++)
        {
            int distance = Math.Min(Math.Min(x - region.Left, region.Right - 1 - x),
                Math.Min(y - region.Top, region.Bottom - 1 - y));
            if (distance >= width) continue;
            double mix = (double)distance / width;
            Color a = original.GetPixel(x, y), b = target.GetPixel(x, y);
            target.SetPixel(x, y, Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * mix),
                (int)Math.Round(a.R + (b.R - a.R) * mix),
                (int)Math.Round(a.G + (b.G - a.G) * mix),
                (int)Math.Round(a.B + (b.B - a.B) * mix)));
        }
    }
}
'@
$plan = Get-Content -LiteralPath $PlanFile -Raw -Encoding utf8 | ConvertFrom-Json
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
[void][System.IO.Directory]::CreateDirectory((Join-Path $output 'files'))
$entries = @()
foreach ($asset in $plan.assets) {
    $edited = [System.Drawing.Image]::FromFile($asset.edited)
    $canvas = [System.Drawing.Bitmap]::new([int]$asset.width, [int]$asset.height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    $original = $null
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $full = [System.Drawing.Rectangle]::new(0, 0, $canvas.Width, $canvas.Height)
        if ($asset.contentRect) {
            $full = [System.Drawing.Rectangle]::new($asset.contentRect[0], $asset.contentRect[1], $asset.contentRect[2], $asset.contentRect[3])
        }
        if ($asset.regions) {
            $original = [System.Drawing.Image]::FromFile($asset.original)
            if ($original.Width -ne $canvas.Width -or $original.Height -ne $canvas.Height) { throw 'Original canvas mismatch' }
            $graphics.DrawImageUnscaled($original, 0, 0)
            foreach ($region in $asset.regions) {
                $destination = [System.Drawing.Rectangle]::new($region[0], $region[1], $region[2], $region[3])
                $source = [System.Drawing.Rectangle]::new(
                    [int]($region[0] * $edited.Width / $canvas.Width), [int]($region[1] * $edited.Height / $canvas.Height),
                    [int]($region[2] * $edited.Width / $canvas.Width), [int]($region[3] * $edited.Height / $canvas.Height))
                $graphics.DrawImage($edited, $destination, $source, [System.Drawing.GraphicsUnit]::Pixel)
                $graphics.Flush()
                [ImageRegionBlend]::Feather([System.Drawing.Bitmap]$original, $canvas, $destination, 12)
            }
        } else {
            if ($asset.whiteMask) {
                $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
                $matrix = [System.Drawing.Imaging.ColorMatrix]::new()
                $matrix.Matrix00 = 0; $matrix.Matrix11 = 0; $matrix.Matrix22 = 0
                $matrix.Matrix40 = 1; $matrix.Matrix41 = 1; $matrix.Matrix42 = 1
                $attributes.SetColorMatrix($matrix)
                try { $graphics.DrawImage($edited, $full, 0, 0, $edited.Width, $edited.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes) }
                finally { $attributes.Dispose() }
            } else {
                $graphics.DrawImage($edited, $full)
            }
        }
        if ($asset.outline) { $graphics.Flush(); [ImageRegionBlend]::Outline($canvas, [int]$asset.outline) }
        $stream = [System.IO.MemoryStream]::new()
        try { $canvas.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png); $bytes = $stream.ToArray() }
        finally { $stream.Dispose() }
        $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $relative = "files/$hash.png"
        [System.IO.File]::WriteAllBytes((Join-Path $output $relative), $bytes)
        $entries += [ordered]@{
            kind = 'sprite'; name = $asset.name; scope = [string]$asset.scope
            width = [int]$asset.width; height = [int]$asset.height
            file = $relative; sha256 = $hash; bytes = $bytes.Length
        }
        Write-Output "$($asset.name) $($asset.scope) $($asset.width)x$($asset.height) $hash"
    } finally {
        $graphics.Dispose(); $canvas.Dispose(); $edited.Dispose()
        if ($original) { $original.Dispose() }
    }
}
[System.IO.File]::WriteAllText((Join-Path $output 'catalog.json'),
    ((@{ version = 1; entries = @($entries) } | ConvertTo-Json -Depth 12) -replace "`r`n", "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
