param(
    [Parameter(Mandatory)]
    [string]$InputPath,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot '..\assets\AMacQ.ico'
}

Add-Type -AssemblyName System.Drawing

if (!(Test-Path -LiteralPath $InputPath)) {
    throw "找不到 PNG 图标源文件：$InputPath"
}

$outputDirectory = Split-Path -Parent $OutputPath
if (!(Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $InputPath))
try {
    $images = @()
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $scale = [Math]::Min($size / $source.Width, $size / $source.Height)
            $width = [int][Math]::Round($source.Width * $scale)
            $height = [int][Math]::Round($source.Height * $scale)
            $left = [int][Math]::Floor(($size - $width) / 2)
            $top = [int][Math]::Floor(($size - $height) / 2)
            $graphics.DrawImage($source, $left, $top, $width, $height)
            $stream = [System.IO.MemoryStream]::new()
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $images += ,$stream.ToArray()
            $stream.Dispose()
        } finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create))
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)
        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) { $writer.Write($image) }
    } finally {
        $writer.Dispose()
    }
} finally {
    $source.Dispose()
}

Write-Host "已生成图标：$OutputPath"
