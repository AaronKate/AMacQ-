[CmdletBinding()]
param(
    [string]$InputPath,
    [string]$OutputPath,
    [ValidateRange(0, 255)]
    [int]$WhiteThreshold = 245,
    [ValidateRange(1, 4096)]
    [int]$OutputSize = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Test-NearWhite {
    param(
        [System.Drawing.Color]$Color,
        [int]$Threshold
    )

    $Color.R -ge $Threshold -and $Color.G -ge $Threshold -and $Color.B -ge $Threshold
}

function Remove-EdgeConnectedWhite {
    param(
        [Parameter(Mandatory)]
        [string]$InputPath,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [ValidateRange(0, 255)]
        [int]$WhiteThreshold = 245,

        [ValidateRange(1, 4096)]
        [int]$OutputSize = 0
    )

    if (!(Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        throw "Input image not found: $InputPath"
    }

    $source = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $InputPath))
    try {
        $bitmap = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.DrawImage($source, 0, 0, $source.Width, $source.Height)
            } finally {
                $graphics.Dispose()
            }

            $width = $bitmap.Width
            $height = $bitmap.Height
            $visited = New-Object 'bool[,]' $width, $height
            $queue = New-Object 'System.Collections.Generic.Queue[System.Drawing.Point]'

            foreach ($x in 0..($width - 1)) {
                $queue.Enqueue([System.Drawing.Point]::new($x, 0))
                if ($height -gt 1) { $queue.Enqueue([System.Drawing.Point]::new($x, $height - 1)) }
            }
            foreach ($y in 1..($height - 2)) {
                $queue.Enqueue([System.Drawing.Point]::new(0, $y))
                if ($width -gt 1) { $queue.Enqueue([System.Drawing.Point]::new($width - 1, $y)) }
            }

            while ($queue.Count -gt 0) {
                $point = $queue.Dequeue()
                if ($visited[$point.X, $point.Y]) { continue }
                $visited[$point.X, $point.Y] = $true

                $color = $bitmap.GetPixel($point.X, $point.Y)
                if (!(Test-NearWhite $color $WhiteThreshold)) { continue }

                $bitmap.SetPixel($point.X, $point.Y, [System.Drawing.Color]::FromArgb(0, $color.R, $color.G, $color.B))
                foreach ($neighbor in @(
                    [System.Drawing.Point]::new($point.X - 1, $point.Y),
                    [System.Drawing.Point]::new($point.X + 1, $point.Y),
                    [System.Drawing.Point]::new($point.X, $point.Y - 1),
                    [System.Drawing.Point]::new($point.X, $point.Y + 1)
                )) {
                    if ($neighbor.X -ge 0 -and $neighbor.X -lt $width -and
                        $neighbor.Y -ge 0 -and $neighbor.Y -lt $height -and
                        !$visited[$neighbor.X, $neighbor.Y]) {
                        $queue.Enqueue($neighbor)
                    }
                }
            }

            $outputDirectory = Split-Path -Parent $OutputPath
            if ($outputDirectory) {
                New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
            }

            if ($OutputSize -gt 0 -and ($width -ne $OutputSize -or $height -ne $OutputSize)) {
                $resized = New-Object System.Drawing.Bitmap $OutputSize, $OutputSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try {
                    $resizedGraphics = [System.Drawing.Graphics]::FromImage($resized)
                    try {
                        $resizedGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                        $resizedGraphics.DrawImage($bitmap, 0, 0, $OutputSize, $OutputSize)
                    } finally {
                        $resizedGraphics.Dispose()
                    }
                    $resized.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
                } finally {
                    $resized.Dispose()
                }
            } else {
                $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
            }
        } finally {
            $bitmap.Dispose()
        }
    } finally {
        $source.Dispose()
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    if (!$InputPath -or !$OutputPath) {
        throw 'InputPath and OutputPath are required when running this script.'
    }
    Remove-EdgeConnectedWhite -InputPath $InputPath -OutputPath $OutputPath -WhiteThreshold $WhiteThreshold -OutputSize $OutputSize
}
