[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputPath,

    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (!(Test-Path -LiteralPath $InputPath -PathType Leaf)) {
    throw "Input image not found: $InputPath"
}

function Set-RoundedMaskAlpha {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$CornerRadius = 24,
        [int]$EdgeInset = 2
    )

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $left = $EdgeInset
    $top = $EdgeInset
    $right = $width - 1 - $EdgeInset
    $bottom = $height - 1 - $EdgeInset
    $radius = [double]$CornerRadius

    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $inside = $x -ge $left -and $x -le $right -and $y -ge $top -and $y -le $bottom
            if ($inside) {
                $nearestX = [Math]::Min([Math]::Max($x, $left + $CornerRadius), $right - $CornerRadius)
                $nearestY = [Math]::Min([Math]::Max($y, $top + $CornerRadius), $bottom - $CornerRadius)
                $distance = [Math]::Sqrt([Math]::Pow($x - $nearestX, 2) + [Math]::Pow($y - $nearestY, 2))
                $inside = $distance -le $radius
            }

            if (!$inside) {
                $color = $Bitmap.GetPixel($x, $y)
                $Bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
            }
        }
    }
}

$source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $InputPath))
try {
    $output = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($output)
        try {
            $graphics.Clear([System.Drawing.Color]::Black)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, 256, 256)
        } finally {
            $graphics.Dispose()
        }

        Set-RoundedMaskAlpha -Bitmap $output -CornerRadius 24 -EdgeInset 2

        $outputDirectory = Split-Path -Parent $OutputPath
        if ($outputDirectory) {
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        }
        $output.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $output.Dispose()
    }
} finally {
    $source.Dispose()
}
