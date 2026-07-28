$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
$content = Get-Content -Raw $sourcePath

if ($content -notmatch 'x:Key="AppBackgroundBrush"') {
    throw 'A shared application background brush is required.'
}

foreach ($requiredColor in @(
    'x:Key="TextPrimaryColor"',
    'x:Key="TextBodyColor"',
    'x:Key="TextSecondaryColor"',
    'x:Key="TextListColor"',
    'x:Key="AccentForegroundColor"',
    'x:Key="BorderControlColor"',
    'x:Key="BorderFocusColor"',
    'x:Key="BorderDividerColor"',
    'x:Key="DangerCloseHoverColor"',
    'x:Key="AnimationAppStartColor"',
    'x:Key="AnimationAppEndColor"',
    'x:Key="AnimationPanelStartColor"',
    'x:Key="AnimationPanelEndColor"',
    'x:Key="AnimationInputStartColor"',
    'x:Key="AnimationInputEndColor"',
    'x:Key="AnimationPopupStartColor"',
    'x:Key="AnimationPopupEndColor"'
)) {
    if ($content -notmatch [regex]::Escape($requiredColor)) {
        throw "The centralized theme palette requires $requiredColor."
    }
}

foreach ($requiredBrush in @(
    'x:Key="PrimaryTextBrush"',
    'x:Key="BodyTextBrush"',
    'x:Key="SecondaryTextBrush"',
    'x:Key="ListItemTextBrush"',
    'x:Key="AccentForegroundBrush"',
    'x:Key="FocusBorderBrush"',
    'x:Key="DividerBrush"',
    'x:Key="PanelOutlineBrush"',
    'x:Key="TitleBarDividerBrush"',
    'x:Key="ScrollTrackBrush"',
    'x:Key="ScrollThumbBrush"',
    'x:Key="ScrollThumbHoverBrush"',
    'x:Key="WindowCloseHoverBrush"'
)) {
    if ($content -notmatch [regex]::Escape($requiredBrush)) {
        throw "The centralized theme brush set requires $requiredBrush."
    }
}

if ($content -notmatch '\$Window\.Resources\[''AnimationAppStartColor''\]' -or
    $content -notmatch '\$Window\.Resources\[''AnimationPopupEndColor''\]') {
    throw 'The background animation must obtain its target colors from named resources.'
}

foreach ($requiredResource in @(
    'x:Key="SidebarSurfaceBrush"',
    'x:Key="ContentSurfaceBrush"',
    'x:Key="PanelSurfaceBrush"',
    'x:Key="InputSurfaceBrush"',
    'x:Key="PopupSurfaceBrush"',
    'x:Key="ControlBorderBrush"'
)) {
    if ($content -notmatch [regex]::Escape($requiredResource)) {
        throw "The deep-ocean theme requires resource $requiredResource."
    }
}

if ($content -notmatch '<Color x:Key="SurfaceAppStartColor">#263B68</Color>' -or
    $content -notmatch '<Color x:Key="SurfaceAppEndColor">#090E20</Color>' -or
    $content -notmatch '<GradientStop Color="\{StaticResource SurfaceAppStartColor\}" Offset="0"/>' -or
    $content -notmatch '<GradientStop Color="\{StaticResource SurfaceAppEndColor\}" Offset="1"/>') {
    throw 'The application background must use palette-backed deep-ocean colors.'
}

if ($content -notmatch '<RadialGradientBrush x:Key="AuroraGlowBrush"' -or
    $content -notmatch '<Color x:Key="AuroraGlowStartColor">#3022D3EE</Color>' -or
    $content -notmatch 'Color="\{StaticResource AuroraGlowStartColor\}"') {
    throw 'The application background must include a palette-backed cyan aurora glow.'
}

if ($content -notmatch 'Name="MinimizeBtn"' -or
    $content -notmatch '\$minimizeBtn\.Add_Click' -or
    $content -notmatch 'WindowState\s*=\s*\[Windows\.WindowState\]::Minimized') {
    throw 'A working minimize button is required.'
}

if ($content -notmatch '<Grid Background="\{StaticResource AppBackgroundBrush\}">\s*<Grid.RowDefinitions>[\s\S]*?</Grid.RowDefinitions>\s*<Border Grid.RowSpan="2" Background="\{StaticResource AuroraGlowBrush\}" IsHitTestVisible="False"/>' -or
    $content -notmatch '<Border Name="SidebarPanel" Grid.Column="0" Background="\{StaticResource SidebarSurfaceBrush\}"' -or
    $content -notmatch '<Grid Name="ContentPanel" Grid.Column="1" Background="\{StaticResource ContentSurfaceBrush\}">') {
    throw 'The application must paint the deep-ocean background, aurora glow, sidebar, and content surfaces.'
}

if ($content -notmatch '<Setter Property="Background" Value="\{DynamicResource InputSurfaceBrush\}"/>' -or
    $content -notmatch '<Setter Property="BorderBrush" Value="\{DynamicResource ControlBorderBrush\}"/>') {
    throw 'Text and combo controls must use the shared deep-ocean input surface and border.'
}

if ($content -notmatch '<Setter TargetName="ComboToggleBorder" Property="Background" Value="\{DynamicResource ControlHoverBrush\}"/>' -or
    $content -notmatch '<Border Background="\{DynamicResource PopupSurfaceBrush\}" BorderBrush="\{DynamicResource ControlBorderBrush\}"') {
    throw 'The combo box and popup must use the themed hover and popup surfaces.'
}

if ($content -match 'Name="ScanlineOverlay"' -or $content -match '<DrawingBrush') {
    throw 'The deep-ocean theme must not include a scanline overlay or drawing-brush texture.'
}

$productionUiStart = $content.IndexOf('<Window xmlns=')
$productionUiEnd = $content.IndexOf("'@", $productionUiStart)
$productionUi = $content.Substring($productionUiStart, $productionUiEnd - $productionUiStart)
$resourcesEnd = $productionUi.IndexOf('</Window.Resources>') + '</Window.Resources>'.Length
$templateAndLayout = $productionUi.Substring($resourcesEnd)
if ($templateAndLayout -match '(?i)(?<![A-Za-z0-9])#[0-9A-F]{3,8}(?![A-Za-z0-9])' -or
    $templateAndLayout -match 'Value="White"' -or
    $templateAndLayout -match 'Foreground="White"') {
    throw 'Control templates and layout must reference semantic brushes instead of color literals.'
}

$buildFieldCardsStart = $content.IndexOf('function Build-FieldCards')
$buildFieldCardsEnd = $content.IndexOf('function Fill-WeaponFields', $buildFieldCardsStart)
$buildFieldCards = $content.Substring($buildFieldCardsStart, $buildFieldCardsEnd - $buildFieldCardsStart)
if ($buildFieldCards -match 'BrushConverter|ConvertFromString|#[0-9A-Fa-f]{3,8}') {
    throw 'Build-FieldCards must resolve theme brushes from resources without hard-coded colors.'
}

foreach ($requiredRuntimeBrush in @(
    "FindResource('PrimaryTextBrush')",
    "FindResource('BodyTextBrush')",
    "FindResource('FocusBorderBrush')",
    "FindResource('DividerBrush')"
)) {
    if (!$buildFieldCards.Contains($requiredRuntimeBrush)) {
        throw "Build-FieldCards must resolve $requiredRuntimeBrush."
    }
}

if ($content -notmatch 'Foreground="\{StaticResource SecondaryTextBrush\}"' -or
    $content -notmatch '<ListBox Name="WeaponList" BorderThickness="0" Background="Transparent"\s*FontSize="14" Foreground="\{DynamicResource ListItemTextBrush\}"') {
    throw 'Labels and the weapon list must use readable deep-ocean text resources.'
}

if ($content -notmatch '<Border Grid.ColumnSpan="2" BorderBrush="\{DynamicResource TitleBarDividerBrush\}" BorderThickness="0,0,0,1" IsHitTestVisible="False"/>') {
    throw 'A subtle semantic title-bar separator is required.'
}

if ($content -notmatch '\$appBackgroundBrush = \[Windows\.Media\.LinearGradientBrush\]\$Window\.Resources\[''AppBackgroundBrush''\]' -or
    $content -notmatch '\$appBackgroundBrush\.GradientStops\[0\]; Color = \$animationAppStartColor' -or
    $content -notmatch '\$appBackgroundBrush\.GradientStops\[1\]; Color = \$animationAppEndColor') {
    throw 'The shared application brush must retain palette-backed animation targets.'
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
