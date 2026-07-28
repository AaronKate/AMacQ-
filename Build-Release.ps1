$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'AMacQGuiEditor.ps1'
$outputPath = Join-Path $PSScriptRoot 'dist\AMacQ配置编辑器.exe'
$outputDirectory = Split-Path -Parent $outputPath

if (!(Test-Path -LiteralPath $scriptPath)) {
    throw "找不到主程序脚本：$scriptPath"
}

if (!(Get-Module -ListAvailable -Name ps2exe)) {
    Install-Module -Name ps2exe -Scope CurrentUser -Force
}

Import-Module ps2exe -Force

if (!(Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Force
}

Invoke-ps2exe -InputFile $scriptPath -OutputFile $outputPath -NoConsole

if (!(Test-Path -LiteralPath $outputPath)) {
    throw "打包失败，未生成 EXE：$outputPath"
}

if ((Get-Item -LiteralPath $outputPath).Length -eq 0) {
    throw "打包失败，EXE 文件为空：$outputPath"
}

Write-Host "已生成：$outputPath"
