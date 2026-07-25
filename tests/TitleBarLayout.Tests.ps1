$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
$content = Get-Content -Raw $sourcePath

if ($content -notmatch 'x:Key="AppBackgroundBrush"') {
    throw 'A shared application background brush is required.'
}

if ($content -notmatch 'Name="MinimizeBtn"' -or
    $content -notmatch '\$minimizeBtn\.Add_Click' -or
    $content -notmatch 'WindowState\s*=\s*\[Windows\.WindowState\]::Minimized') {
    throw 'A working minimize button is required.'
}

if ($content -notmatch '<Grid Background="\{StaticResource AppBackgroundBrush\}">' -or
    $content -notmatch '<Border Name="SidebarPanel" Grid.Column="0" Background="Transparent"' -or
    $content -notmatch '<Grid Name="ContentPanel" Grid.Column="1" Background="Transparent">') {
    throw 'The application background must be painted once behind transparent title and content regions.'
}

if ($content -notmatch '<Border Grid.ColumnSpan="2" BorderBrush="#FFFFFF" BorderThickness="0,0,0,1" Opacity="0.18" IsHitTestVisible="False"/>') {
    throw 'A subtle non-interactive title-bar separator is required.'
}

if ($content -notmatch '\$appBackgroundBrush = \[Windows\.Media\.LinearGradientBrush\]\$Window\.Resources\[''AppBackgroundBrush''\]' -or
    $content -notmatch '\$appBackgroundBrush\.GradientStops\[0\]; Color = ''#3659A3''' -or
    $content -notmatch '\$appBackgroundBrush\.GradientStops\[1\]; Color = ''#151C4A''') {
    throw 'The shared application brush must be animated directly.'
}

$xamlStart = $content.IndexOf('$xaml = @' + "'")
$xamlEnd = $content.IndexOf("'@", $xamlStart)
if ($xamlStart -lt 0 -or $xamlEnd -lt 0) {
    throw 'The window XAML could not be located.'
}

Add-Type -AssemblyName PresentationFramework
$xaml = $content.Substring($content.IndexOf("`n", $xamlStart) + 1, $xamlEnd - $content.IndexOf("`n", $xamlStart) - 1)
[void][Windows.Markup.XamlReader]::Parse($xaml)
