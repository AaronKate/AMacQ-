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

$iconPath = Join-Path $PSScriptRoot '..\assets\AMacQ.ico'
if (!(Test-Path -LiteralPath $iconPath)) {
    throw 'The build must include assets\AMacQ.ico.'
}

if ($content -notmatch '\$iconPath\s*=\s*Join-Path\s+\$PSScriptRoot\s+''assets\\AMacQ\.ico''') {
    throw 'The build script must resolve assets\AMacQ.ico.'
}

if ($content -notmatch 'Test-Path\s+-LiteralPath\s+\$iconPath') {
    throw 'The build script must validate the icon before packaging.'
}

if ($content -notmatch '-iconFile\s+\$iconPath') {
    throw 'The build script must embed the icon with ps2exe.'
}

$appPath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
$appContent = Get-Content -LiteralPath $appPath -Raw -Encoding UTF8
if ($appContent -notmatch 'function\s+Set-WindowIcon') {
    throw 'The application must provide a focused runtime window icon helper.'
}

if ($appContent -notmatch '\[Diagnostics\.Process\]::GetCurrentProcess\(\)\.MainModule\.FileName') {
    throw 'The application must resolve its current executable path when running from the packaged application.'
}

if ($appContent -notmatch '\[System\.Drawing\.Icon\]::ExtractAssociatedIcon\(\$executablePath\)') {
    throw 'The application must use the EXE-embedded icon when running from the packaged application.'
}

if ($appContent -notmatch '\$iconPath\s*=\s*Join-Path\s+\$PSScriptRoot\s+''assets\\AMacQ\.ico''') {
    throw 'The application must fall back to assets\AMacQ.ico when running the development PS1.'
}

$parseIndex = $appContent.IndexOf('[Windows.Markup.XamlReader]::Parse($xaml)')
$iconCallIndex = $appContent.IndexOf('Set-WindowIcon $window')
$showDialogIndex = $appContent.IndexOf('$window.ShowDialog()')
if ($parseIndex -lt 0 -or $iconCallIndex -le $parseIndex -or $iconCallIndex -ge $showDialogIndex) {
    throw 'The runtime icon helper must run after XAML parsing and before the window is shown.'
}

if ($appContent -match '<Window[\s\S]*?Icon=') {
    throw 'The WPF Window must not reference an external icon in XAML.'
}
