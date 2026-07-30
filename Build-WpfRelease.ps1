$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'src\AMacQConfigEditor\AMacQConfigEditor.csproj'
$outputPath = Join-Path $PSScriptRoot 'dist\wpf'

dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --property:PublishSingleFile=true `
    --property:IncludeNativeLibrariesForSelfExtract=true `
    --property:IncludeAllContentForSelfExtract=true `
    --property:DebugType=None `
    --property:DebugSymbols=false `
    --output $outputPath

if ($LASTEXITCODE -ne 0) {
    throw "WPF 发布失败，退出代码：$LASTEXITCODE"
}
