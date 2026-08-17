$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Invoke-Obfuscation.ps1')

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\AMacQConfigEditor\AMacQConfigEditor.csproj'
$outputPath = Join-Path $projectRoot 'dist\net48'
$authorBuildPath = Join-Path $projectRoot 'author-tools\build'
$verificationFileName = "AMacQ$([char]0x914D)$([char]0x7F6E)$([char]0x7F16)$([char]0x8F91)$([char]0x5668)-$([char]0x9A8C)$([char]0x8BC1)$([char]0x7248).exe"
$authorFileName = "AMacQ$([char]0x914D)$([char]0x7F6E)$([char]0x7F16)$([char]0x8F91)$([char]0x5668)-$([char]0x4F5C)$([char]0x8005)$([char]0x7248).exe"

if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Recurse -Force }
if (Test-Path -LiteralPath $authorBuildPath) { Remove-Item -LiteralPath $authorBuildPath -Recurse -Force }

dotnet build $projectPath --configuration Release --property:DebugType=None --property:DebugSymbols=false --output $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Verification build failed.' }
Move-Item -LiteralPath (Join-Path $outputPath 'AMacQConfigEditor.exe') -Destination (Join-Path $outputPath $verificationFileName) -Force

dotnet build $projectPath --configuration Release --property:AuthorEdition=true --property:DebugType=None --property:DebugSymbols=false --output $authorBuildPath
if ($LASTEXITCODE -ne 0) { throw 'Author build failed.' }
Move-Item -LiteralPath (Join-Path $authorBuildPath 'AMacQConfigEditor.exe') -Destination (Join-Path $outputPath $authorFileName) -Force
Remove-Item -LiteralPath $authorBuildPath -Recurse -Force

Get-ChildItem -LiteralPath $outputPath -Filter '*.exe.config' -File | Remove-Item -Force

Invoke-ApplicationObfuscation -ApplicationPath (Join-Path $outputPath $verificationFileName)
Invoke-ApplicationObfuscation -ApplicationPath (Join-Path $outputPath $authorFileName)

Write-Host "Created: $(Join-Path $outputPath $verificationFileName)"
Write-Host "Created: $(Join-Path $outputPath $authorFileName)"
