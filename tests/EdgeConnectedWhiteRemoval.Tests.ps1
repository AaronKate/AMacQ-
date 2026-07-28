$ErrorActionPreference = 'Stop'

$toolPath = Join-Path $PSScriptRoot '..\tools\Remove-EdgeConnectedWhite.ps1'
if (!(Test-Path -LiteralPath $toolPath)) {
    throw 'The edge-connected white removal tool is required.'
}

. $toolPath
Add-Type -AssemblyName System.Drawing

$tempRoot = Join-Path $env:TEMP "AMacQ-EdgeWhite-$PID"
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
$inputPath = Join-Path $tempRoot 'input.png'
$outputPath = Join-Path $tempRoot 'output.png'

try {
    $bitmap = New-Object System.Drawing.Bitmap 7, 7, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 7; $y++) {
            for ($x = 0; $x -lt 7; $x++) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::White)
            }
        }
        for ($y = 2; $y -le 4; $y++) {
            for ($x = 2; $x -le 4; $x++) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 0, 64, 0))
            }
        }
        $bitmap.SetPixel(3, 3, [System.Drawing.Color]::White)
        $bitmap.Save($inputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $bitmap.Dispose()
    }

    Remove-EdgeConnectedWhite -InputPath $inputPath -OutputPath $outputPath
    $output = [System.Drawing.Bitmap]::FromFile($outputPath)
    try {
        if ($output.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) {
            throw 'The processed icon source must be 32-bit ARGB.'
        }
        if ($output.GetPixel(0, 0).A -ne 0) {
            throw 'Edge-connected white background must become transparent.'
        }
        if ($output.GetPixel(3, 3).A -ne 255) {
            throw 'Internal white highlights isolated by the icon must remain opaque.'
        }
        if ($output.GetPixel(2, 2).A -ne 255) {
            throw 'Non-white icon pixels must remain opaque.'
        }
    } finally {
        $output.Dispose()
    }
} finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
