$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'src\AMacQConfigEditor\AMacQConfigEditor.csproj'
$outputPath = Join-Path $PSScriptRoot 'dist\net48'

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet build $projectPath `
    --configuration Release `
    --property:DebugType=None `
    --property:DebugSymbols=false `
    --output $outputPath

if ($LASTEXITCODE -eq 0) {
    $executablePath = Join-Path $outputPath 'AMacQConfigEditor.exe'
    if (!(Test-Path -LiteralPath $executablePath)) {
        throw "Build did not produce the expected executable: $executablePath"
    }

    $configurationPath = "$executablePath.config"
    if (Test-Path -LiteralPath $configurationPath) {
        Remove-Item -LiteralPath $configurationPath -Force
    }

    $unexpectedFiles = @(Get-ChildItem -LiteralPath $outputPath -File | Where-Object { $_.Name -ne 'AMacQConfigEditor.exe' })
    if ($unexpectedFiles.Count -gt 0) {
        throw "Build produced unexpected files: $($unexpectedFiles.Name -join ', ')"
    }

    Write-Host "Created: $executablePath"
}

if ($LASTEXITCODE -ne 0) {
    throw "WPF 发布失败，退出代码：$LASTEXITCODE"
}
