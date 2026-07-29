$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot '..'
$iconPath = Join-Path $projectRoot 'assets\AMacQ.ico'
$converterPath = Join-Path $projectRoot 'tools\Convert-Icon.ps1'
$sourceConverterPath = Join-Path $projectRoot 'tools\Convert-IconSource.ps1'
$sourcePath = Join-Path $projectRoot 'assets\AMacQ-source.png'

if (!(Test-Path -LiteralPath $sourceConverterPath)) {
    throw 'The source icon converter is required.'
}

Remove-Item -LiteralPath $iconPath -Force -ErrorAction SilentlyContinue
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $converterPath -InputPath $sourcePath
if ($LASTEXITCODE -ne 0) {
    throw "The documented converter invocation failed with exit code $LASTEXITCODE."
}
if (!(Test-Path -LiteralPath $iconPath)) {
    throw 'The documented converter invocation must create assets\AMacQ.ico by default.'
}

if (!(Test-Path -LiteralPath $iconPath)) {
    throw 'The AMacQ ICO resource is required.'
}

Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Bitmap]::FromFile($sourcePath)
try {
    if ($sourceImage.Width -ne 256 -or $sourceImage.Height -ne 256) {
        throw 'The source icon must be 256 by 256 pixels.'
    }
    if ($sourceImage.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb) {
        throw 'The source icon must use 32-bit ARGB pixels.'
    }
    $corner = $sourceImage.GetPixel(0, 0)
    if ($corner.A -ne 0) {
        throw 'The source icon corners must be transparent outside the rounded mask.'
    }

    if ($sourceImage.GetPixel(16, 16).A -ne 0) {
        throw 'The source icon must retain a visibly rounded mask at taskbar sizes.'
    }

    $topCenter = $sourceImage.GetPixel(128, 8)
    if ($topCenter.A -ne 255) {
        throw 'The rounded mask must preserve the dark background and green border inside its top edge.'
    }

    $center = $sourceImage.GetPixel(128, 128)
    if ($center.A -ne 255 -or $center.G -le $center.R -or $center.G -le $center.B) {
        throw 'The source icon must preserve the green lightning-triangle center.'
    }
} finally {
    $sourceImage.Dispose()
}

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($iconPath)
if (!$icon) {
    throw 'The AMacQ ICO resource could not be read.'
}

$requiredSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$bytes = [System.IO.File]::ReadAllBytes($iconPath)
$iconCount = [BitConverter]::ToUInt16($bytes, 4)
$actualSizes = @()
for ($index = 0; $index -lt $iconCount; $index++) {
    $entryOffset = 6 + ($index * 16)
    $width = $bytes[$entryOffset]
    $height = $bytes[$entryOffset + 1]
    if ($width -eq 0) { $width = 256 }
    if ($height -eq 0) { $height = 256 }
    if ($width -ne $height) { throw "Icon entry $index is not square." }
    $imageOffset = [BitConverter]::ToUInt32($bytes, $entryOffset + 12)
    if ($bytes[$imageOffset] -eq 0x89 -and $bytes[$imageOffset + 1] -eq 0x50 -and
        $bytes[$imageOffset + 2] -eq 0x4E -and $bytes[$imageOffset + 3] -eq 0x47) {
        throw "Icon entry $index uses PNG encoding, which ps2exe cannot embed reliably."
    }
    $actualSizes += $width
}

foreach ($size in $requiredSizes) {
    if ($actualSizes -notcontains $size) {
        throw "The ICO must contain a ${size}x${size} image."
    }
}
