$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\Build-Release.ps1'
if (!(Test-Path -LiteralPath $sourcePath)) {
    throw 'The EXE build script is required.'
}

$content = Get-Content -LiteralPath $sourcePath -Raw

if ($content -notmatch '\$scriptPath\s*=\s*Join-Path\s+\$PSScriptRoot\s+''AMacQGuiEditor\.ps1''') {
    throw 'The build script must package the root AMacQGuiEditor.ps1 source file.'
}

if ($content -notmatch '\$outputPath\s*=\s*Join-Path\s+\$PSScriptRoot\s+''dist\\AMacQ配置编辑器\.exe''') {
    throw 'The build script must use dist\\AMacQ配置编辑器.exe as its output path.'
}

if ($content -notmatch 'Install-Module\s+-Name\s+ps2exe\s+-Scope\s+CurrentUser') {
    throw 'The build script must install ps2exe only for the current user.'
}

if ($content -notmatch 'Invoke-ps2exe' -or
    $content -notmatch '-NoConsole' -or
    $content -notmatch '-InputFile\s+\$scriptPath' -or
    $content -notmatch '-OutputFile\s+\$outputPath') {
    throw 'The build script must use ps2exe to create a console-free executable.'
}

if ($content -notmatch 'Test-Path\s+-LiteralPath\s+\$outputPath') {
    throw 'The build script must verify that the executable was generated.'
}

$readmePath = Join-Path $PSScriptRoot '..\README.md'
$readme = Get-Content -LiteralPath $readmePath -Raw -Encoding UTF8

if ($readme -notmatch 'AMacQ配置编辑器\.exe') {
    throw 'README must document the EXE launcher.'
}

if ($readme -notmatch 'Build-Release\.ps1') {
    throw 'README must document the EXE build script.'
}

$gitignorePath = Join-Path $PSScriptRoot '..\.gitignore'
$gitignore = Get-Content -LiteralPath $gitignorePath -Raw -Encoding UTF8
if ($gitignore -notmatch '(?m)^dist/\r?$') {
    throw 'Generated dist output must be ignored by Git.'
}
