$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Invoke-Obfuscation.ps1')

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'src\AMacQConfigEditor\AMacQConfigEditor.csproj'
$licenseGeneratorProjectPath = Join-Path $projectRoot 'tools\AMacQLicenseGenerator\AMacQLicenseGenerator.csproj'
$outputPath = Join-Path $projectRoot 'dist\net48'
$licenseGeneratorPath = Join-Path $projectRoot 'author-tools\AMacQLicenseGenerator.exe'
$applicationPath = Join-Path $outputPath 'AMacQ配置编辑器-验证版.exe'
$authorApplicationPath = Join-Path $outputPath 'AMacQ配置编辑器-作者版.exe'
$authorBuildPath = Join-Path $projectRoot 'author-tools\build'

# Use Unicode code points so the published Chinese filenames are stable even when this script is opened in a legacy code page.
$verificationFileName = "AMacQ$([char]0x914D)$([char]0x7F6E)$([char]0x7F16)$([char]0x8F91)$([char]0x5668)-$([char]0x9A8C)$([char]0x8BC1)$([char]0x7248).exe"
$authorFileName = "AMacQ$([char]0x914D)$([char]0x7F6E)$([char]0x7F16)$([char]0x8F91)$([char]0x5668)-$([char]0x4F5C)$([char]0x8005)$([char]0x7248).exe"
$applicationPath = Join-Path $outputPath $verificationFileName
$authorApplicationPath = Join-Path $outputPath $authorFileName

function Stop-RunningBuildTarget {
    param([string]$ProcessName, [string]$TargetPath)

    $targetProcess = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $TargetPath }

    foreach ($process in $targetProcess) {
        Write-Host "Stopping running build target (PID $($process.Id)): $TargetPath"
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
    }
}

Stop-RunningBuildTarget 'AMacQ配置编辑器-验证版' $applicationPath
Stop-RunningBuildTarget 'AMacQ配置编辑器-作者版' $authorApplicationPath
Stop-RunningBuildTarget 'AMacQLicenseGenerator' $licenseGeneratorPath

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet build $projectPath `
    --configuration Release `
    --property:DebugType=None `
    --property:DebugSymbols=false `
    --output $outputPath

if ($LASTEXITCODE -ne 0) {
    throw "主程序发布构建失败，退出代码：$LASTEXITCODE"
}

$builtApplicationPath = Join-Path $outputPath 'AMacQConfigEditor.exe'
if (!(Test-Path -LiteralPath $builtApplicationPath)) {
    throw "构建未生成验证版程序：$builtApplicationPath"
}
Move-Item -LiteralPath $builtApplicationPath -Destination $applicationPath -Force

if (Test-Path -LiteralPath $authorBuildPath) {
    Remove-Item -LiteralPath $authorBuildPath -Recurse -Force
}

dotnet build $projectPath `
    --configuration Release `
    --property:AuthorEdition=true `
    --property:DebugType=None `
    --property:DebugSymbols=false `
    --output $authorBuildPath

if ($LASTEXITCODE -ne 0) {
    throw "作者自用版构建失败，退出代码：$LASTEXITCODE"
}

$builtAuthorApplicationPath = Join-Path $authorBuildPath 'AMacQConfigEditor.exe'
if (!(Test-Path -LiteralPath $builtAuthorApplicationPath)) {
    throw "构建未生成作者自用版：$builtAuthorApplicationPath"
}
Copy-Item -LiteralPath $builtAuthorApplicationPath -Destination $authorApplicationPath -Force
Remove-Item -LiteralPath $authorBuildPath -Recurse -Force

dotnet build $licenseGeneratorProjectPath `
    --configuration Release `
    --property:DebugType=None `
    --property:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "授权签发工具构建失败，退出代码：$LASTEXITCODE"
}

$builtLicenseGeneratorPath = Join-Path $projectRoot 'tools\AMacQLicenseGenerator\bin\Release\net48\AMacQLicenseGenerator.exe'
if (!(Test-Path -LiteralPath $builtLicenseGeneratorPath)) {
    throw "构建未生成授权签发工具：$builtLicenseGeneratorPath"
}

Copy-Item -LiteralPath $builtLicenseGeneratorPath -Destination $licenseGeneratorPath -Force

$executablePath = $applicationPath
if (!(Test-Path -LiteralPath $executablePath)) {
    throw "构建未生成主程序：$executablePath"
}

# The build writes AMacQConfigEditor.exe.config before the EXE is renamed.
Get-ChildItem -LiteralPath $outputPath -Filter '*.exe.config' -File | Remove-Item -Force

$expectedFiles = @('AMacQ配置编辑器-验证版.exe', 'AMacQ配置编辑器-作者版.exe')
$expectedFiles = @($verificationFileName, $authorFileName)
$unexpectedFiles = @(Get-ChildItem -LiteralPath $outputPath -File | Where-Object { $_.Name -notin $expectedFiles })
if ($unexpectedFiles.Count -gt 0) {
    throw "发布目录包含意外文件：$($unexpectedFiles.Name -join ', ')"
}

Invoke-ApplicationObfuscation -ApplicationPath $applicationPath
Invoke-ApplicationObfuscation -ApplicationPath $authorApplicationPath

$(Get-Item -LiteralPath $executablePath).LastWriteTime = Get-Date

Write-Host "Created: $executablePath"
Write-Host "Created: $licenseGeneratorPath"
Write-Host "Created: $authorApplicationPath"
