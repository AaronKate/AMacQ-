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

if ($content -notmatch '<Image Name="TitleBarIcon" Width="20" Height="20" Margin="12,0,8,0"') {
    throw 'The custom title bar must contain a 20px TitleBarIcon with the required spacing.'
}

if ($content -notmatch '<StackPanel Orientation="Horizontal">\s*<Image Name="TitleBarIcon"[\s\S]*?<TextBlock Text="AMacQ Configuration Editor"') {
    throw 'The title bar icon must appear before the application title text.'
}

$xamlParseIndex = $content.IndexOf('[Windows.Markup.XamlReader]::Parse($xaml)')
$setWindowIconIndex = if ($xamlParseIndex -ge 0) { $content.IndexOf('Set-WindowIcon $window', $xamlParseIndex) } else { -1 }
$titleBarIconLookupIndex = if ($setWindowIconIndex -ge 0) { $content.IndexOf('$titleBarIcon = $window.FindName(''TitleBarIcon'')', $setWindowIconIndex) } else { -1 }
$titleBarIconAssignmentIndex = if ($titleBarIconLookupIndex -ge 0) { $content.IndexOf('$titleBarIcon.Source = $window.Icon', $titleBarIconLookupIndex) } else { -1 }
$showDialogIndex = $content.IndexOf('$window.ShowDialog()')
if ($xamlParseIndex -lt 0 -or
    $setWindowIconIndex -le $xamlParseIndex -or
    $titleBarIconLookupIndex -le $setWindowIconIndex -or
    $titleBarIconAssignmentIndex -le $titleBarIconLookupIndex -or
    $titleBarIconAssignmentIndex -ge $showDialogIndex) {
    throw 'The title bar icon source must be synchronized after Window.Icon is loaded and before the window is shown.'
}

$xamlStart = $content.IndexOf('$xaml = @' + "'")
$xamlEnd = $content.IndexOf("'@", $xamlStart)
if ($xamlStart -lt 0 -or $xamlEnd -lt 0) {
    throw 'The window XAML could not be located.'
}

Add-Type -AssemblyName PresentationFramework
$xaml = $content.Substring($content.IndexOf("`n", $xamlStart) + 1, $xamlEnd - $content.IndexOf("`n", $xamlStart) - 1)
[void][Windows.Markup.XamlReader]::Parse($xaml)
