[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$branding = Join-Path $repository 'media/branding'
$source = Join-Path $branding 'osiris-logo-source.jpg'

# Format/size conversion only: preserve the user's artwork, background and full
# square composition. The separately supplied loading PNG is never rewritten.
Add-Type -AssemblyName System.Drawing
$original = [Drawing.Image]::FromFile($source)
try {
    $frames = [Collections.Generic.List[byte[]]]::new()
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    foreach ($size in (@(512) + $sizes)) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $attributes = [Drawing.Imaging.ImageAttributes]::new()
                try {
                    $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
                    $graphics.DrawImage($original, [Drawing.Rectangle]::new(0,0,$size,$size), 0,0,$original.Width,$original.Height, [Drawing.GraphicsUnit]::Pixel, $attributes)
                } finally { $attributes.Dispose() }
            } finally { $graphics.Dispose() }
            if ($size -eq 512) {
                $bitmap.Save((Join-Path $branding 'osiris-logo.png'), [Drawing.Imaging.ImageFormat]::Png)
            } else {
                $memory = [IO.MemoryStream]::new()
                try { $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png); $frames.Add($memory.ToArray()) }
                finally { $memory.Dispose() }
            }
        } finally { $bitmap.Dispose() }
    }
    $stream = [IO.File]::Create((Join-Path $branding 'osiris.ico'))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write($frame) }
    } finally { $writer.Dispose(); $stream.Dispose() }
} finally { $original.Dispose() }
Write-Output 'Generated the shared PNG and nine-size Windows ICO from media/branding/osiris-logo-source.jpg; loading PNG unchanged.'
