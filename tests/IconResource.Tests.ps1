$ErrorActionPreference = 'Stop'

$projectRoot = Join-Path $PSScriptRoot '..'
$iconPath = Join-Path $projectRoot 'assets\AMacQ.ico'
$converterPath = Join-Path $projectRoot 'tools\Convert-Icon.ps1'
$sourcePath = Join-Path $projectRoot 'assets\AMacQ-source.png'

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
    $actualSizes += $width
}

foreach ($size in $requiredSizes) {
    if ($actualSizes -notcontains $size) {
        throw "The ICO must contain a ${size}x${size} image."
    }
}
