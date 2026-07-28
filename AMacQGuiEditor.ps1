$ErrorActionPreference = 'Stop'

# ================================================================
# Constants
# ================================================================
$script:TargetFiles = @('KeyBindings', 'Sensitivity')
$script:ValuePattern = '-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})'

$script:PressOptions = @(
    [pscustomobject]@{ Text='鼠标左键'; Num='1' }
    [pscustomobject]@{ Text='按住右键 + 鼠标左键'; Num='3' }
)

$script:ModeSwitchOptions = @(
    [pscustomobject]@{ Text='Scroll Lock'; Value='scrolllock' }
    [pscustomobject]@{ Text='Caps Lock'; Value='capslock' }
    [pscustomobject]@{ Text='Num Lock'; Value='numlock' }
)

$script:MouseProfiles = [ordered]@{
    '通用双侧键鼠标' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G102' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G304 / G305' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G Pro Wireless（GPW）' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
        @{ Text='右侧后退键(7)'; Num='7' }
        @{ Text='右侧前进键(8)'; Num='8' }
    )
    'G Pro X Superlight（GPX）' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G402' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G502 Hero' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
    'G502 X' = @(
        @{ Text='左侧后退键(4)'; Num='4' }
        @{ Text='左侧前进键(5)'; Num='5' }
    )
}

function Get-MouseProfileBindings {
    param([string]$ModelName)
    if (!$script:MouseProfiles.Contains($ModelName)) { $ModelName = '通用双侧键鼠标' }
    @([pscustomobject]@{ Text='无按键(0)'; Num='0'; IsLegacy=$false }) + 
        @($script:MouseProfiles[$ModelName] | ForEach-Object {
        [pscustomobject]@{ Text = $_.Text; Num = $_.Num; IsLegacy = $false }
    })
}

function New-KeyBindingItems {
    param([string]$ModelName, [string]$CurrentValue)
    $items = @(Get-MouseProfileBindings $ModelName)
    if ($CurrentValue -and !($items.Num -contains $CurrentValue)) {
        $items += [pscustomobject]@{ Text = "当前配置($CurrentValue)"; Num = $CurrentValue; IsLegacy = $true }
    }
    $items
}

function Set-KeyComboItems {
    param($ComboBox, [string]$ModelName, [string]$CurrentValue)
    $ComboBox.Items.Clear()
    $sel = -1; $i = 0
    foreach ($item in (New-KeyBindingItems $ModelName $CurrentValue)) {
        [void]$ComboBox.Items.Add($item)
        if ($item.Num -eq $CurrentValue) { $sel = $i }; $i++
    }
    $ComboBox.SelectedIndex = $sel
}

$script:FieldDefs = @(
    @{ File='KeyBindings'; VarSuffix='qq1156777787';         Label='无修饰键';     SavePattern='^[0-9]$';                                 HelpText='请选择一个按键';   Type='Combo' }
    @{ File='KeyBindings'; VarSuffix='qq1156777787_second';   Label='按住 Alt';      SavePattern='^[0-9]$';                                 HelpText='请选择一个按键';   Type='Combo' }
    @{ File='KeyBindings'; VarSuffix='Third';                 Label='按住 Ctrl';     SavePattern='^[0-9]$';                                 HelpText='请选择一个按键';   Type='Combo' }
    @{ File='Sensitivity'; VarSuffix='qq1156777787_X';        Label='灵敏度 X';      SavePattern='^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$'; HelpText='请输入数值（支持负数，最多两位小数）' }
    @{ File='Sensitivity'; VarSuffix='qq1156777787_Y';        Label='灵敏度 Y';      SavePattern='^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$'; HelpText='请输入数值（支持负数，最多两位小数）' }
    @{ File='Sensitivity'; VarSuffix='qq1156777787_add_X';    Label='灵敏度 增幅 X'; SavePattern='^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$'; HelpText='请输入数值（支持负数，最多两位小数）' }
    @{ File='Sensitivity'; VarSuffix='qq1156777787_add_Y';    Label='灵敏度 增幅 Y'; SavePattern='^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$'; HelpText='请输入数值（支持负数，最多两位小数）' }
)

# ================================================================
# Lua parsing
# ================================================================
function Get-LuaAssignments {
    param([string]$Content)
    [regex]::Matches($Content, "(?m)^\s*(?<n>[A-Za-z0-9_]+)\s*=\s*(?<v>$script:ValuePattern)") |
        ForEach-Object { [pscustomobject]@{ Name = $_.Groups['n'].Value; Value = $_.Groups['v'].Value } }
}

function Get-PrimaryWeapons {
    param([string]$Content)
    $seen = @{}
    $result = @()
    $kgSuffixes = ($script:FieldDefs | Where-Object File -eq $script:TargetFiles[0] | ForEach-Object { [regex]::Escape($_.VarSuffix) }) -join '|'
    $pattern = "^(?<w>[A-Za-z0-9]+)_(?:$kgSuffixes)$"
    Get-LuaAssignments $Content | ForEach-Object {
        if ($_.Name -match $pattern -and !$seen[$Matches.w]) {
            $seen[$Matches.w] = $true
            $result += $Matches.w
        }
    }
    $result
}

function Get-WeaponListItems {
    param($ConfigModel)

    $assignments = @{}
    foreach ($assignment in Get-LuaAssignments $ConfigModel.Files['KeyBindings'].Content) {
        $assignments[$assignment.Name] = $assignment.Value
    }

    $bindingFormats = @(
        @{ Suffix = 'qq1156777787'; Prefix = '' }
        @{ Suffix = 'qq1156777787_second'; Prefix = 'Alt+' }
        @{ Suffix = 'Third'; Prefix = 'Ctrl+' }
    )

    foreach ($weapon in $ConfigModel.Weapons) {
        $parts = @()
        foreach ($binding in $bindingFormats) {
            $variableName = "${weapon}_$($binding.Suffix)"
            $value = $assignments[$variableName]
            if ($value -match '^[1-9]$') {
                $parts += "$($binding.Prefix)$value"
            }
        }

        [pscustomobject]@{
            Name = $weapon
            BindingSummary = $parts -join ' · '
            HasBindingSummary = $parts.Count -gt 0
        }
    }
}

function ConvertTo-DecimalValue {
    param([string]$Value, [string]$Pattern = "^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$", [string]$Hint = '请输入有效数值。')
    if ($Value -notmatch $Pattern) { throw $Hint }
    $Value
}

function Set-LuaValue {
    param([string]$Content, [string]$VarName, [string]$NewValue)
    $escaped = [regex]::Escape($VarName)
    $match = [regex]::Match($Content, "(?m)^(?<l>\s*$escaped\s*=\s*)$script:ValuePattern")
    if (!$match.Success) { throw "Variable not found in content: $VarName" }
    $Content.Substring(0, $match.Index) + $match.Groups['l'].Value +
        $NewValue + $Content.Substring($match.Index + $match.Length)
}

function Get-LuaStringValue {
    param([string]$Content, [string]$VarName)
    $escaped = [regex]::Escape($VarName)
    $match = [regex]::Match($Content, "(?m)^\s*$escaped\s*=\s*`"(?<v>[^`"]*)`"")
    if ($match.Success) { return $match.Groups['v'].Value }
    $null
}

function Set-LuaStringValue {
    param([string]$Content, [string]$VarName, [string]$NewValue)
    $escaped = [regex]::Escape($VarName)
    $match = [regex]::Match($Content, "(?m)^(?<l>\s*$escaped\s*=\s*)`"[^`"]*`"")
    if (!$match.Success) { throw "Variable not found in content: $VarName" }
    $Content.Substring(0, $match.Index) + $match.Groups['l'].Value +
        '"' + $NewValue + '"' + $Content.Substring($match.Index + $match.Length)
}

# ================================================================
# File I/O – atomic writes with encoding preservation
# ================================================================
function Get-FileEncoding {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.UTF8Encoding]::new($true)       # UTF-8 BOM
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.UnicodeEncoding]::new($false, $true)  # UTF-16 LE
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.BigEndianUnicodeEncoding]::new($true)  # UTF-16 BE
    }
    return [System.Text.UTF8Encoding]::new($false)           # UTF-8 no BOM
}

function Read-LuaFile {
    param([string]$Path)
    $enc = Get-FileEncoding $Path
    [pscustomobject]@{
        Path     = $Path
        Content  = [System.IO.File]::ReadAllText($Path, $enc)
        Encoding = $enc
    }
}

function Save-LuaFile {
    param([string]$Path, [string]$Content, [System.Text.Encoding]$Encoding)
    $tempPath = "$Path.writing." + [System.Guid]::NewGuid().ToString('N').Substring(0, 8)
    try {
        [System.IO.File]::WriteAllText($tempPath, $Content, $Encoding)
        Move-Item -Force $tempPath $Path
    } catch {
        if (Test-Path $tempPath) { Remove-Item $tempPath -Force -ErrorAction SilentlyContinue }
        throw
    }
}

# ================================================================
# Configuration model
# ================================================================
function Read-AMacQConfig {
    param([string]$KeyBindingsPath, [string]$SensitivityPath)
    if (!(Test-Path -LiteralPath $KeyBindingsPath)) { throw "找不到按键配置文件：$KeyBindingsPath" }
    if (!(Test-Path -LiteralPath $SensitivityPath)) { throw "找不到灵敏度配置文件：$SensitivityPath" }
    $model = @{
        Files       = @{}
        SourcePaths = @{}
        Weapons     = @()
    }
    $model.SourcePaths[$script:TargetFiles[0]] = $KeyBindingsPath
    $model.SourcePaths[$script:TargetFiles[1]] = $SensitivityPath
    $model.Files[$script:TargetFiles[0]] = Read-LuaFile $KeyBindingsPath
    $model.Files[$script:TargetFiles[1]] = Read-LuaFile $SensitivityPath
    $model.Weapons = @(Get-PrimaryWeapons $model.Files[$script:TargetFiles[0]].Content)
    $model
}

function Start-AnimatedBackground {
    param(
        [Windows.Window]$Window
    )

    $appBackgroundBrush = [Windows.Media.LinearGradientBrush]$Window.Resources['AppBackgroundBrush']
    $duration = [Windows.Duration]::new([TimeSpan]::FromSeconds(8))
    $animationAppStartColor = [Windows.Media.Color]$Window.Resources['AnimationAppStartColor']
    $animationAppEndColor = [Windows.Media.Color]$Window.Resources['AnimationAppEndColor']
    $animationPanelStartColor = [Windows.Media.Color]$Window.Resources['AnimationPanelStartColor']
    $animationPanelEndColor = [Windows.Media.Color]$Window.Resources['AnimationPanelEndColor']
    $animationInputStartColor = [Windows.Media.Color]$Window.Resources['AnimationInputStartColor']
    $animationInputEndColor = [Windows.Media.Color]$Window.Resources['AnimationInputEndColor']
    $animationPopupStartColor = [Windows.Media.Color]$Window.Resources['AnimationPopupStartColor']
    $animationPopupEndColor = [Windows.Media.Color]$Window.Resources['AnimationPopupEndColor']
    $animations = @(
        @{ Stop = $appBackgroundBrush.GradientStops[0]; Color = $animationAppStartColor }
        @{ Stop = $appBackgroundBrush.GradientStops[1]; Color = $animationAppEndColor }
    )
    foreach ($surface in @(
        @{ Key = 'PanelSurfaceBrush'; Colors = @($animationPanelStartColor, $animationPanelEndColor) }
        @{ Key = 'InputSurfaceBrush'; Colors = @($animationInputStartColor, $animationInputEndColor) }
        @{ Key = 'PopupSurfaceBrush'; Colors = @($animationPopupStartColor, $animationPopupEndColor) }
    )) {
        $surfaceBrush = [Windows.Media.LinearGradientBrush]$Window.Resources[$surface.Key].Clone()
        $Window.Resources[$surface.Key] = $surfaceBrush
        $animations += @(
            @{ Stop = $surfaceBrush.GradientStops[0]; Color = $surface.Colors[0] }
            @{ Stop = $surfaceBrush.GradientStops[1]; Color = $surface.Colors[1] }
        )
    }
    foreach ($item in $animations) {
        $animation = [Windows.Media.Animation.ColorAnimation]::new()
        $animation.To = [Windows.Media.Color]$item.Color
        $animation.Duration = $duration
        $animation.AutoReverse = $true
        $animation.RepeatBehavior = [Windows.Media.Animation.RepeatBehavior]::Forever
        $item.Stop.BeginAnimation([Windows.Media.GradientStop]::ColorProperty, $animation)
    }

}

# ================================================================
# UI – field cards builder
# ================================================================
function Build-FieldCards {
    param($FieldCardsGrid)

    $primaryTextBrush = $FieldCardsGrid.FindResource('PrimaryTextBrush')
    $bodyTextBrush = $FieldCardsGrid.FindResource('BodyTextBrush')
    $secondaryTextBrush = $FieldCardsGrid.FindResource('SecondaryTextBrush')
    $panelSurfaceBrush = $FieldCardsGrid.FindResource('PanelSurfaceBrush')
    $inputSurfaceBrush = $FieldCardsGrid.FindResource('InputSurfaceBrush')
    $controlBorderBrush = $FieldCardsGrid.FindResource('ControlBorderBrush')
    $dividerBrush = $FieldCardsGrid.FindResource('DividerBrush')
    $focusBorderBrush = $FieldCardsGrid.FindResource('FocusBorderBrush')
    $FieldCardsGrid.Children.Clear()
    $groups = @{}
    foreach ($field in $script:FieldDefs) {
        if (!$groups.ContainsKey($field.File)) { $groups[$field.File] = @() }
        $groups[$field.File] += $field
    }

    $inputBoxes = @{}
    foreach ($file in $script:TargetFiles) {
        if (!$groups.ContainsKey($file)) { continue }

        $section = New-Object Windows.Controls.StackPanel
        $section.Margin = '0,0,8,0'

        $header = New-Object Windows.Controls.TextBlock
        $header.Text = if ($file -eq $script:TargetFiles[0]) { '按键' } else { '灵敏度' }
        $header.FontWeight = 'SemiBold'; $header.FontSize = 12
        $header.Foreground = $secondaryTextBrush
        $header.Margin = '0,0,0,8'
        [void]$section.Children.Add($header)

        $list = New-Object Windows.Controls.StackPanel
        $list.Background = [Windows.Media.Brushes]::Transparent
        $list.Margin = '0'

        $outer = New-Object Windows.Controls.Border
        $outer.Background = $panelSurfaceBrush
        $outer.BorderBrush = $controlBorderBrush
        $outer.BorderThickness = '1'
        $outer.CornerRadius = 10
        $outer.Margin = '0'
        $outer.Child = $list
        [void]$section.Children.Add($outer)

        $fields = @($groups[$file])
        for ($i = 0; $i -lt $fields.Count; $i++) {
            $field = $fields[$i]
            $row = New-Object Windows.Controls.Grid
            $row.Height = 44
            $row.ColumnDefinitions.Add((New-Object Windows.Controls.ColumnDefinition -Property @{ Width='*' }))
            $row.ColumnDefinitions.Add((New-Object Windows.Controls.ColumnDefinition -Property @{ Width='150' }))

            $label = New-Object Windows.Controls.TextBlock
            $label.Text = $field.Label; $label.FontSize = 13
            $label.Foreground = $bodyTextBrush
            $label.VerticalAlignment = 'Center'; $label.Margin = '14,0,8,0'
            [Windows.Controls.Grid]::SetColumn($label, 0)
            [void]$row.Children.Add($label)

            if ($field.Type -eq 'Combo') {
                $ctrl = New-Object Windows.Controls.ComboBox
                $ctrl.DisplayMemberPath = 'Text'
                $ctrl.VerticalContentAlignment = 'Center'
                $ctrl.Padding = '5,0'
                $ctrl.ItemContainerStyle = $FieldCardsGrid.FindResource('DarkComboBoxItem')
            } else {
                $ctrl = New-Object Windows.Controls.TextBox
                $ctrl.CaretBrush = $focusBorderBrush; $ctrl.Padding = '8,2'
            }
            $ctrl.Height = 30; $ctrl.Width = 140; $ctrl.FontSize = 13
            $styleKey = if ($field.Type -eq 'Combo') { 'DarkComboBox' } else { 'DarkTextBox' }
            $ctrl.Style = $FieldCardsGrid.FindResource($styleKey)
            $ctrl.Background = $inputSurfaceBrush
            $ctrl.BorderBrush = $controlBorderBrush
            $ctrl.BorderThickness = '1'; $ctrl.Foreground = $primaryTextBrush
            $ctrl.VerticalAlignment = 'Center'; $ctrl.HorizontalAlignment = 'Right'; $ctrl.Margin = '0,0,10,0'
            [Windows.Controls.Grid]::SetColumn($ctrl, 1)
            [void]$row.Children.Add($ctrl)

            if ($i -lt $fields.Count - 1) {
                $line = New-Object Windows.Controls.Border
                $line.Height = 1; $line.Background = $dividerBrush
                $line.HorizontalAlignment = 'Stretch'; $line.Margin = '14,43,0,0'
                [void]$row.Children.Add($line)
            }
            [void]$list.Children.Add($row)
            $inputBoxes["$file|$($field.VarSuffix)"] = $ctrl
        }
        if ($file -eq $script:TargetFiles[1]) { $section.Margin = '8,0,0,0' }
        [void]$FieldCardsGrid.Children.Add($section)
    }
    $inputBoxes
}

    function Fill-WeaponFields {
        param($Model, $InputBoxes, $Weapon, $SelectedLabel, $SelectedWeaponLabel, [string]$ModelName)
        $SelectedLabel.Text = '枪械：'; $SelectedWeaponLabel.Text = $Weapon

    # Build an assignment lookup per file
    $assignMaps = @{}
    foreach ($file in $script:TargetFiles) {
        $assignMaps[$file] = @{}
        Get-LuaAssignments $Model.Files[$file].Content | ForEach-Object {
            $assignMaps[$file][$_.Name] = $_.Value
        }
    }

    foreach ($field in $script:FieldDefs) {
        $varName = "${Weapon}_$($field.VarSuffix)"
        $key = "$($field.File)|$($field.VarSuffix)"
        $ctrl = $InputBoxes[$key]

        if ($assignMaps[$field.File].ContainsKey($varName)) {
            $val = $assignMaps[$field.File][$varName]
            if ($field.Type -eq 'Combo') {
                Set-KeyComboItems $ctrl $ModelName $val
            } else {
                $ctrl.Text = $val
            }
            $ctrl.IsEnabled = $true
        } else {
            if ($field.Type -eq 'Combo') { $ctrl.Items.Clear(); $ctrl.SelectedIndex = -1 }
            else { $ctrl.Text = '' }
            $ctrl.IsEnabled = $false
        }
    }
}

function Set-WindowIcon {
    param([Windows.Window]$Window)

    $executablePath = [Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    if ([IO.Path]::GetExtension($executablePath) -eq '.exe') {
        $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($executablePath)
        if ($icon) {
            $Window.Icon = [Windows.Interop.Imaging]::CreateBitmapSourceFromHIcon(
                $icon.Handle,
                [Windows.Int32Rect]::Empty,
                [Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions())
            return
        }
    }

    $iconPath = Join-Path $PSScriptRoot 'assets\AMacQ.ico'
    if (Test-Path -LiteralPath $iconPath) {
        $Window.Icon = [Windows.Media.Imaging.BitmapFrame]::Create([Uri]$iconPath)
    }
}

# ================================================================
# Start-Gui – main entry point
# ================================================================
function Start-Gui {
    Add-Type -AssemblyName PresentationFramework, System.Windows.Forms

	    $xaml = @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="AMacQ Configuration Editor" Height="600" Width="860"
        MinHeight="520" MinWidth="760" WindowStartupLocation="CenterScreen"
        WindowStyle="None" ResizeMode="CanResize"
        Background="{DynamicResource AppBackgroundBrush}" Foreground="{DynamicResource PrimaryTextBrush}" FontFamily="Segoe UI">
  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="38" ResizeBorderThickness="6" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/>
  </shell:WindowChrome.WindowChrome>
  <Window.Resources>
    <!-- Theme palette: change only these Color resources to retheme the application. -->
    <Color x:Key="SurfaceAppStartColor">#263B68</Color>
    <Color x:Key="SurfaceAppEndColor">#090E20</Color>
    <Color x:Key="AuroraGlowStartColor">#3022D3EE</Color>
    <Color x:Key="AuroraGlowMiddleColor">#1022D3EE</Color>
    <Color x:Key="AuroraGlowEndColor">#0022D3EE</Color>
    <Color x:Key="AccentCyanColor">#22D3EE</Color>
    <Color x:Key="AccentIndigoColor">#6366F1</Color>
    <Color x:Key="SurfaceSidebarStartColor">#1A243F</Color>
    <Color x:Key="SurfaceSidebarEndColor">#10192D</Color>
    <Color x:Key="SurfaceContentStartColor">#1B3F62</Color>
    <Color x:Key="SurfaceContentEndColor">#0B1428</Color>
    <Color x:Key="SurfacePanelStartColor">#1A3556</Color>
    <Color x:Key="SurfacePanelEndColor">#0F2038</Color>
    <Color x:Key="SurfaceInputStartColor">#132440</Color>
    <Color x:Key="SurfaceInputEndColor">#0D1B32</Color>
    <Color x:Key="SurfacePopupStartColor">#142942</Color>
    <Color x:Key="SurfacePopupEndColor">#091526</Color>
    <Color x:Key="TextPrimaryColor">#F7F2FF</Color>
    <Color x:Key="TextBodyColor">#EDE7FF</Color>
    <Color x:Key="TextSecondaryColor">#B9CAE0</Color>
    <Color x:Key="TextListColor">#DCEBFA</Color>
    <Color x:Key="AccentForegroundColor">#FFFFFFFF</Color>
    <Color x:Key="BorderControlColor">#4E8FAE</Color>
    <Color x:Key="BorderFocusColor">#5DD7FF</Color>
    <Color x:Key="BorderDividerColor">#31506E</Color>
    <Color x:Key="BorderPanelColor">#6488C4</Color>
    <Color x:Key="ControlHoverColor">#1E5274</Color>
    <Color x:Key="ControlPressedColor">#17415F</Color>
    <Color x:Key="ScrollTrackColor">#10243A</Color>
    <Color x:Key="ScrollThumbColor">#5577C8</Color>
    <Color x:Key="ScrollThumbHoverColor">#71E1FF</Color>
    <Color x:Key="DangerCloseHoverColor">#C42B4B</Color>
    <Color x:Key="TitleBarDividerColor">#30FFFFFF</Color>
    <Color x:Key="AnimationAppStartColor">#315C8F</Color>
    <Color x:Key="AnimationAppEndColor">#0C142B</Color>
    <Color x:Key="AnimationPanelStartColor">#204A70</Color>
    <Color x:Key="AnimationPanelEndColor">#122944</Color>
    <Color x:Key="AnimationInputStartColor">#183153</Color>
    <Color x:Key="AnimationInputEndColor">#10213B</Color>
    <Color x:Key="AnimationPopupStartColor">#1A3554</Color>
    <Color x:Key="AnimationPopupEndColor">#0B1B30</Color>

    <SolidColorBrush x:Key="PrimaryTextBrush" Color="{StaticResource TextPrimaryColor}"/>
    <SolidColorBrush x:Key="BodyTextBrush" Color="{StaticResource TextBodyColor}"/>
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="{StaticResource TextSecondaryColor}"/>
    <SolidColorBrush x:Key="ListItemTextBrush" Color="{StaticResource TextListColor}"/>
    <SolidColorBrush x:Key="AccentForegroundBrush" Color="{StaticResource AccentForegroundColor}"/>
    <SolidColorBrush x:Key="ControlBorderBrush" Color="{StaticResource BorderControlColor}"/>
    <SolidColorBrush x:Key="FocusBorderBrush" Color="{StaticResource BorderFocusColor}"/>
    <SolidColorBrush x:Key="DividerBrush" Color="{StaticResource BorderDividerColor}"/>
    <SolidColorBrush x:Key="PanelOutlineBrush" Color="{StaticResource BorderPanelColor}"/>
    <SolidColorBrush x:Key="ControlHoverBrush" Color="{StaticResource ControlHoverColor}"/>
    <SolidColorBrush x:Key="ControlPressedBrush" Color="{StaticResource ControlPressedColor}"/>
    <SolidColorBrush x:Key="ScrollTrackBrush" Color="{StaticResource ScrollTrackColor}"/>
    <SolidColorBrush x:Key="ScrollThumbBrush" Color="{StaticResource ScrollThumbColor}"/>
    <SolidColorBrush x:Key="ScrollThumbHoverBrush" Color="{StaticResource ScrollThumbHoverColor}"/>
    <SolidColorBrush x:Key="WindowCloseHoverBrush" Color="{StaticResource DangerCloseHoverColor}"/>
    <SolidColorBrush x:Key="TitleBarDividerBrush" Color="{StaticResource TitleBarDividerColor}"/>

    <LinearGradientBrush x:Key="AppBackgroundBrush" StartPoint="0,0" EndPoint="1,1">
      <GradientStop Color="{StaticResource SurfaceAppStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfaceAppEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <RadialGradientBrush x:Key="AuroraGlowBrush" Center="0.12,0.08" GradientOrigin="0.12,0.08" RadiusX="0.78" RadiusY="0.70">
      <GradientStop Color="{StaticResource AuroraGlowStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource AuroraGlowMiddleColor}" Offset="0.38"/>
      <GradientStop Color="{StaticResource AuroraGlowEndColor}" Offset="1"/>
    </RadialGradientBrush>
    <LinearGradientBrush x:Key="AccentGradientBrush" StartPoint="0,0" EndPoint="1,0">
      <GradientStop Color="{StaticResource AccentCyanColor}" Offset="0"/>
      <GradientStop Color="{StaticResource AccentIndigoColor}" Offset="1"/>
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="SidebarSurfaceBrush" StartPoint="0,0" EndPoint="1,1" Opacity="0.82">
      <GradientStop Color="{StaticResource SurfaceSidebarStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfaceSidebarEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="ContentSurfaceBrush" StartPoint="0,0" EndPoint="1,1" Opacity="0.26">
      <GradientStop Color="{StaticResource SurfaceContentStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfaceContentEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="PanelSurfaceBrush" StartPoint="0,0" EndPoint="1,1" Opacity="0.62">
      <GradientStop Color="{StaticResource SurfacePanelStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfacePanelEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="InputSurfaceBrush" StartPoint="0,0" EndPoint="1,1">
      <GradientStop Color="{StaticResource SurfaceInputStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfaceInputEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <LinearGradientBrush x:Key="PopupSurfaceBrush" StartPoint="0,0" EndPoint="1,1" Opacity="0.99">
      <GradientStop Color="{StaticResource SurfacePopupStartColor}" Offset="0"/>
      <GradientStop Color="{StaticResource SurfacePopupEndColor}" Offset="1"/>
    </LinearGradientBrush>
    <Style x:Key="DarkScrollThumb" TargetType="Thumb">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Thumb">
            <Border x:Name="ScrollThumb" Background="{DynamicResource ScrollThumbBrush}" CornerRadius="5" Margin="1"/>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="ScrollThumb" Property="Background" Value="{DynamicResource ScrollThumbHoverBrush}"/>
              </Trigger>
              <Trigger Property="IsDragging" Value="True">
                <Setter TargetName="ScrollThumb" Property="Background" Value="{DynamicResource FocusBorderBrush}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="DarkScrollTrackButton" TargetType="RepeatButton">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="RepeatButton">
            <Border x:Name="TrackSurface" Background="Transparent" CornerRadius="4"/>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType="ScrollBar">
      <Setter Property="Width" Value="11"/>
      <Setter Property="Background" Value="{DynamicResource ScrollTrackBrush}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ScrollBar">
            <Border Background="{TemplateBinding Background}" CornerRadius="5" Margin="1">
              <Track x:Name="PART_Track" IsDirectionReversed="True">
                <Track.DecreaseRepeatButton>
                  <RepeatButton Command="ScrollBar.PageUpCommand" Style="{StaticResource DarkScrollTrackButton}"/>
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                  <Thumb Style="{StaticResource DarkScrollThumb}"/>
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                  <RepeatButton Command="ScrollBar.PageDownCommand" Style="{StaticResource DarkScrollTrackButton}"/>
                </Track.IncreaseRepeatButton>
              </Track>
            </Border>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style TargetType="TextBox">
      <Setter Property="Background" Value="{DynamicResource InputSurfaceBrush}"/>
      <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
      <Setter Property="BorderBrush" Value="{DynamicResource ControlBorderBrush}"/>
    </Style>
    <Style TargetType="ComboBox">
      <Setter Property="Background" Value="{DynamicResource InputSurfaceBrush}"/>
      <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
      <Setter Property="BorderBrush" Value="{DynamicResource ControlBorderBrush}"/>
    </Style>
    <Style x:Key="DarkTextBox" TargetType="TextBox">
      <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
      <Setter Property="Background" Value="{DynamicResource InputSurfaceBrush}"/>
      <Setter Property="BorderBrush" Value="{DynamicResource ControlBorderBrush}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="TextBox">
            <Border x:Name="TextBoxBorder" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5">
              <ScrollViewer x:Name="PART_ContentHost" VerticalContentAlignment="Center" Margin="{TemplateBinding Padding}"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="TextBoxBorder" Property="BorderBrush" Value="{DynamicResource ControlHoverBrush}"/></Trigger>
              <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="TextBoxBorder" Property="BorderBrush" Value="{DynamicResource FocusBorderBrush}"/></Trigger>
              <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.52"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="DarkComboBox" TargetType="ComboBox">
      <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
      <Setter Property="Background" Value="{DynamicResource InputSurfaceBrush}"/>
      <Setter Property="BorderBrush" Value="{DynamicResource ControlBorderBrush}"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ComboBox">
            <Grid>
                <ToggleButton x:Name="ToggleButton" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                              BorderThickness="{TemplateBinding BorderThickness}" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <ToggleButton.Template>
                    <ControlTemplate TargetType="ToggleButton">
                      <Border x:Name="ComboToggleBorder" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5"><ContentPresenter/></Border>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ComboToggleBorder" Property="Background" Value="{DynamicResource ControlHoverBrush}"/></Trigger>
                        <Trigger Property="IsChecked" Value="True"><Setter TargetName="ComboToggleBorder" Property="Background" Value="{DynamicResource ControlHoverBrush}"/></Trigger>
                        <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="ComboToggleBorder" Property="BorderBrush" Value="{DynamicResource FocusBorderBrush}"/></Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </ToggleButton.Template>
                  <Grid><TextBlock Margin="9,0,28,0" VerticalAlignment="Center" Foreground="{TemplateBinding Foreground}" Text="{Binding SelectedItem.Text, RelativeSource={RelativeSource TemplatedParent}}"/><Path Data="M 0 0 L 6 0 L 3 4 Z" Fill="{DynamicResource SecondaryTextBrush}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,0"/></Grid>
                </ToggleButton>
              <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True">
                <Border Background="{DynamicResource PopupSurfaceBrush}" BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1" MinWidth="{Binding ActualWidth, ElementName=ToggleButton}">
                  <ScrollViewer MaxHeight="220"><ItemsPresenter/></ScrollViewer>
                </Border>
              </Popup>
            </Grid>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="DarkComboBoxItem" TargetType="ComboBoxItem">
      <Setter Property="Foreground" Value="{DynamicResource ListItemTextBrush}"/>
      <Setter Property="Padding" Value="9,6"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ComboBoxItem">
            <Border x:Name="bd" Background="Transparent" Padding="{TemplateBinding Padding}">
              <TextBlock Text="{Binding Text}" Foreground="{TemplateBinding Foreground}"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlHoverBrush}"/></Trigger>
              <Trigger Property="IsSelected" Value="True"><Setter TargetName="bd" Property="Background" Value="{StaticResource AccentGradientBrush}"/><Setter Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="CenteredComboBoxItem" TargetType="ComboBoxItem">
      <Setter Property="VerticalContentAlignment" Value="Center"/>
      <Setter Property="Padding" Value="5,3"/>
    </Style>
    <Style x:Key="SidebarButton" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="Transparent" CornerRadius="6"
                    Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlHoverBrush}"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlPressedBrush}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="WeaponListItem" TargetType="ListBoxItem">
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="Foreground" Value="{DynamicResource BodyTextBrush}"/>
      <Setter Property="Padding" Value="10,6"/>
      <Setter Property="MinHeight" Value="38"/>
      <Setter Property="Margin" Value="0"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="ListBoxItem">
            <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="6">
              <Grid Margin="{TemplateBinding Padding}">
                <Grid.RowDefinitions>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <TextBlock Text="{Binding Name}"
                           Foreground="{TemplateBinding Foreground}"
                           FontWeight="{TemplateBinding FontWeight}"
                           TextTrimming="CharacterEllipsis"/>
                <TextBlock x:Name="BindingSummary" Grid.Row="1"
                           Margin="0,2,0,0"
                           Text="{Binding BindingSummary}"
                           Foreground="{StaticResource AccentGradientBrush}"
                           FontSize="11"
                           TextWrapping="NoWrap"
                           TextTrimming="CharacterEllipsis"/>
              </Grid>
            </Border>
            <ControlTemplate.Triggers>
              <DataTrigger Binding="{Binding HasBindingSummary}" Value="False">
                <Setter TargetName="BindingSummary" Property="Visibility" Value="Collapsed"/>
              </DataTrigger>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlHoverBrush}"/>
              </Trigger>
              <Trigger Property="IsSelected" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{StaticResource AccentGradientBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/>
                <Setter TargetName="BindingSummary" Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/>
                <Setter Property="FontWeight" Value="SemiBold"/>
              </Trigger>
              <MultiTrigger>
                <MultiTrigger.Conditions>
                  <Condition Property="IsSelected" Value="True"/>
                  <Condition Property="Selector.IsSelectionActive" Value="False"/>
                </MultiTrigger.Conditions>
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlHoverBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/>
                <Setter TargetName="BindingSummary" Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/>
              </MultiTrigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="TitleBarButton" TargetType="Button">
      <Setter Property="Width" Value="46"/>
      <Setter Property="Height" Value="38"/>
      <Setter Property="Foreground" Value="{DynamicResource BodyTextBrush}"/>
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="BorderThickness" Value="0"/>
      <Setter Property="FontFamily" Value="Segoe MDL2 Assets"/>
      <Setter Property="FontSize" Value="10"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="{TemplateBinding Background}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlHoverBrush}"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="bd" Property="Background" Value="{DynamicResource ControlPressedBrush}"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
    <Style x:Key="CloseTitleBarButton" TargetType="Button" BasedOn="{StaticResource TitleBarButton}">
      <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
          <Setter Property="Background" Value="{DynamicResource WindowCloseHoverBrush}"/>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="PrimaryButton" TargetType="Button">
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="7"
                    Padding="{TemplateBinding Padding}">
              <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="bd" Property="Opacity" Value="0.88"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="bd" Property="Opacity" Value="0.72"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>
  <Grid Background="{StaticResource AppBackgroundBrush}">
    <Grid.RowDefinitions>
      <RowDefinition Height="38"/>
      <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
    <Border Grid.RowSpan="2" Background="{StaticResource AuroraGlowBrush}" IsHitTestVisible="False"/>

    <Grid Name="TitleBar" Grid.Row="0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="220"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>

      <Border Background="Transparent">
        <StackPanel Orientation="Horizontal">
          <Image Name="TitleBarIcon" Width="20" Height="20" Margin="12,0,8,0"/>
          <TextBlock Text="AMacQ Configuration Editor"
                     VerticalAlignment="Center" FontSize="13" Foreground="{DynamicResource PrimaryTextBrush}"/>
        </StackPanel>
      </Border>

      <Border Grid.Column="1" Background="Transparent">
        <StackPanel HorizontalAlignment="Right" Orientation="Horizontal"
                    shell:WindowChrome.IsHitTestVisibleInChrome="True">
          <Button Name="MinimizeBtn" Content="&#xE921;" Style="{StaticResource TitleBarButton}"/>
          <Button Name="CloseBtn" Content="&#xE8BB;" Style="{StaticResource CloseTitleBarButton}"/>
        </StackPanel>
      </Border>
      <Border Grid.ColumnSpan="2" BorderBrush="{DynamicResource TitleBarDividerBrush}" BorderThickness="0,0,0,1" IsHitTestVisible="False"/>
    </Grid>

    <Grid Grid.Row="1">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="220"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>

    <!-- macOS System Settings-style navigation sidebar -->
    <Border Name="SidebarPanel" Grid.Column="0" Background="{StaticResource SidebarSurfaceBrush}"
            BorderBrush="{DynamicResource DividerBrush}" BorderThickness="0,0,1,0">
      <Grid Margin="14,20,14,16">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="Auto"/>
          <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
          </Grid.ColumnDefinitions>
             <TextBlock Name="TitleLabel" Text="AMacQ"
                        FontSize="20" FontWeight="SemiBold" Foreground="{StaticResource AccentGradientBrush}"
                        Margin="6,0,0,20"/>
                <Button Name="RefreshBtn" Grid.Column="1" Content="刷新" Style="{StaticResource SidebarButton}"
                        Foreground="{DynamicResource PrimaryTextBrush}" FontSize="12" Padding="8,6" Margin="0,0,0,14"/>
               <Button Name="BrowseBtn" Grid.Column="2" Content="选择文件..." Style="{StaticResource SidebarButton}"
                        Foreground="{DynamicResource PrimaryTextBrush}" FontSize="12" Padding="8,6" Margin="0,0,0,14"/>
        </Grid>

        <StackPanel Grid.Row="1">
          <TextBlock Text="鼠标型号" FontSize="12" Foreground="{StaticResource SecondaryTextBrush}"
                     Margin="6,0,0,5"/>
            <ComboBox Name="MouseModelList" Height="30" FontSize="13"
                      Style="{StaticResource DarkComboBox}"
                         Foreground="{DynamicResource PrimaryTextBrush}" Background="{DynamicResource InputSurfaceBrush}"
                      BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1" Margin="0,0,0,16"
                    VerticalContentAlignment="Center"
                    ItemContainerStyle="{StaticResource DarkComboBoxItem}"/>
        </StackPanel>

          <Grid Grid.Row="2">
            <Grid.RowDefinitions>
              <RowDefinition Height="Auto"/>
              <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            <TextBlock Text="枪械" FontSize="11" FontWeight="SemiBold" Foreground="{StaticResource SecondaryTextBrush}"
                       Margin="6,0,0,7"/>
            <Border Grid.Row="1" BorderBrush="{DynamicResource PanelOutlineBrush}" BorderThickness="1" CornerRadius="8" Background="Transparent">
            <ListBox Name="WeaponList" BorderThickness="0" Background="Transparent"
                     FontSize="14" Foreground="{DynamicResource ListItemTextBrush}"/>
          </Border>
        </Grid>
      </Grid>
    </Border>

    <!-- Detail page -->
    <Grid Name="ContentPanel" Grid.Column="1" Background="{StaticResource ContentSurfaceBrush}">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
      </Grid.RowDefinitions>

      <Border BorderBrush="{DynamicResource DividerBrush}" BorderThickness="0,0,0,1" Padding="32,22,32,20">
           <StackPanel>
                <StackPanel Orientation="Horizontal">
                  <TextBlock Name="SelectedLabel" Text="请选择枪械"
                             FontSize="26" FontWeight="SemiBold" Foreground="{DynamicResource PrimaryTextBrush}"/>
                  <TextBlock Name="SelectedWeaponLabel"
                             FontSize="26" FontWeight="SemiBold" Foreground="{StaticResource AccentGradientBrush}"/>
                </StackPanel>
             <TextBlock Name="LocalOnlyNotice" Text="仅编辑所选目录中的配置文件，不与游戏进程交互"
                        FontSize="11" Foreground="{StaticResource SecondaryTextBrush}" Margin="0,6,0,0"/>
           </StackPanel>
      </Border>

      <Border Grid.Row="1" BorderBrush="{DynamicResource DividerBrush}" BorderThickness="0,0,0,1" Padding="32,14">
        <StackPanel MaxWidth="760" HorizontalAlignment="Left">
          <TextBlock Text="全局设置" FontSize="13" FontWeight="SemiBold" Foreground="{StaticResource SecondaryTextBrush}"
                     Margin="0,0,0,10"/>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="*"/>
              <ColumnDefinition Width="16"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <StackPanel>
              <TextBlock Text="触发方式" FontSize="12" Foreground="{StaticResource SecondaryTextBrush}" Margin="0,0,0,5"/>
                 <ComboBox Name="PressList" Height="30" FontSize="13" DisplayMemberPath="Text"
                           Style="{StaticResource DarkComboBox}"
                              Foreground="{DynamicResource PrimaryTextBrush}" Background="{DynamicResource InputSurfaceBrush}" BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"
                        VerticalContentAlignment="Center"
                        ItemContainerStyle="{StaticResource DarkComboBoxItem}"/>
            </StackPanel>
            <StackPanel Grid.Column="2">
              <TextBlock Text="灵敏度增幅激活键" FontSize="12" Foreground="{StaticResource SecondaryTextBrush}" Margin="0,0,0,5"/>
                 <ComboBox Name="ModeSwitchList" Height="30" FontSize="13" DisplayMemberPath="Text"
                           Style="{StaticResource DarkComboBox}"
                              Foreground="{DynamicResource PrimaryTextBrush}" Background="{DynamicResource InputSurfaceBrush}" BorderBrush="{DynamicResource ControlBorderBrush}" BorderThickness="1"
                        VerticalContentAlignment="Center"
                        ItemContainerStyle="{StaticResource DarkComboBoxItem}"/>
            </StackPanel>
          </Grid>
        </StackPanel>
      </Border>

      <ScrollViewer Grid.Row="2" VerticalScrollBarVisibility="Auto" Padding="32,24,32,20">
        <StackPanel MaxWidth="760" HorizontalAlignment="Left">
          <TextBlock Text="配置详情" FontSize="13" FontWeight="SemiBold" Foreground="{StaticResource SecondaryTextBrush}"
                     Margin="0,0,0,12"/>
          <UniformGrid Name="FieldCards" Columns="2" Rows="1"/>
        </StackPanel>
      </ScrollViewer>

      <Border Grid.Row="3" BorderBrush="{DynamicResource DividerBrush}" BorderThickness="0,1,0,0" Padding="32,14">
        <DockPanel LastChildFill="False">
            <Button Name="SaveBtn" Content="应用" Style="{StaticResource PrimaryButton}"
                     Background="{StaticResource AccentGradientBrush}" Foreground="{DynamicResource AccentForegroundBrush}" FontSize="14" FontWeight="SemiBold"
                  Padding="28,9" DockPanel.Dock="Right"/>
        </DockPanel>
      </Border>
    </Grid>
    </Grid>
  </Grid>
</Window>
'@
    $window = [Windows.Markup.XamlReader]::Parse($xaml)
    Set-WindowIcon $window
    $titleBarIcon = $window.FindName('TitleBarIcon')
    $titleBarIcon.Source = $window.Icon

    # Controls
    $minimizeBtn = $window.FindName('MinimizeBtn')
    $closeBtn = $window.FindName('CloseBtn')
    Start-AnimatedBackground $window
    $refreshBtn   = $window.FindName('RefreshBtn')
    $browseBtn    = $window.FindName('BrowseBtn')
       $weaponList   = $window.FindName('WeaponList')
       $selectedLbl  = $window.FindName('SelectedLabel')
       $selectedWeaponLbl = $window.FindName('SelectedWeaponLabel')
    $fieldCards   = $window.FindName('FieldCards')
    $saveBtn      = $window.FindName('SaveBtn')
    $mouseModelList = $window.FindName('MouseModelList')
    $pressList      = $window.FindName('PressList')
    $modeSwitchList = $window.FindName('ModeSwitchList')

    $weaponList.SetValue([Windows.Controls.ScrollViewer]::VerticalScrollBarVisibilityProperty,
                         [Windows.Controls.ScrollBarVisibility]::Visible)

    # Build field cards from definitions
    $inputBoxes = Build-FieldCards $fieldCards

    # Populate mouse model list
    $script:MouseProfiles.Keys | ForEach-Object { [void]$mouseModelList.Items.Add([pscustomobject]@{ Text = $_; Value = $_ }) }
    $mouseModelList.SelectedItem = @($mouseModelList.Items | Where-Object Value -eq '通用双侧键鼠标')[0]
    $script:PressOptions | ForEach-Object { [void]$pressList.Items.Add($_) }
    $script:ModeSwitchOptions | ForEach-Object { [void]$modeSwitchList.Items.Add($_) }

    # ListBox item style – stable selection highlight when inactive
    $weaponList.ItemContainerStyle = $window.FindResource('WeaponListItem')

    # Application state
    $script:ConfigModel = $null

    # ---- Event handlers -------------------------------------------------
    $refreshWeaponList = {
        param([string]$SelectedWeapon)

        if (!$SelectedWeapon -and $weaponList.SelectedItem) {
            $SelectedWeapon = $weaponList.SelectedItem.Name
        }
        $weaponList.Items.Clear()
        if (!$script:ConfigModel) { return }

        foreach ($item in Get-WeaponListItems $script:ConfigModel) {
            [void]$weaponList.Items.Add($item)
        }

        if ($weaponList.Items.Count) {
            $selectedItem = $weaponList.Items | Where-Object Name -eq $SelectedWeapon | Select-Object -First 1
            if ($selectedItem) {
                $weaponList.SelectedItem = $selectedItem
            } else {
                $weaponList.SelectedIndex = 0
            }
        }
    }

    $loadFiles = {
        param([string]$keyBindingsPath, [string]$sensitivityPath)
        $script:ConfigModel = $null
        & $refreshWeaponList
           $saveBtn.IsEnabled = $false
           $saveBtn.Content = '应用'
           $selectedLbl.Text = '请选择枪械'
           $selectedWeaponLbl.Text = ''

        try {
            $script:ConfigModel = Read-AMacQConfig $keyBindingsPath $sensitivityPath
            $globalContent = $script:ConfigModel.Files[$script:TargetFiles[0]].Content
            $pressValue = Get-LuaAssignments $globalContent | Where-Object Name -eq 'press' | Select-Object -First 1 -ExpandProperty Value
            $modeSwitchValue = Get-LuaStringValue $globalContent 'modeswitch'
            $pressList.SelectedItem = @($pressList.Items | Where-Object Num -eq $pressValue)[0]
            $modeSwitchList.SelectedItem = @($modeSwitchList.Items | Where-Object Value -eq $modeSwitchValue)[0]
            & $refreshWeaponList
            $saveBtn.IsEnabled = $true
        } catch {
            $pressList.SelectedIndex = -1
            $modeSwitchList.SelectedIndex = -1
            $window.Title = 'AMacQ Configuration Editor'
            [System.Windows.MessageBox]::Show("加载配置失败：$($_.Exception.Message)", '错误', 'OK', 'Warning')
        }
    }

    $selectConfigFiles = {
        $keyBindingsDialog = New-Object System.Windows.Forms.OpenFileDialog
        $keyBindingsDialog.Title = '选择第一个配置文件（按键配置）'
        $keyBindingsDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
        $keyBindingsDialog.Multiselect = $false
        if ($keyBindingsDialog.ShowDialog() -ne 'OK') { return }

        $sensitivityDialog = New-Object System.Windows.Forms.OpenFileDialog
        $sensitivityDialog.Title = '选择第二个配置文件（灵敏度配置）'
        $sensitivityDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
        $sensitivityDialog.InitialDirectory = [System.IO.Path]::GetDirectoryName($keyBindingsDialog.FileName)
        $sensitivityDialog.Multiselect = $false
        if ($sensitivityDialog.ShowDialog() -ne 'OK') { return }
        if ($sensitivityDialog.FileName -eq $keyBindingsDialog.FileName) {
            [System.Windows.MessageBox]::Show('请为两个配置角色选择不同的文件。', '提示', 'OK', 'Information')
            return
        }
        & $loadFiles $keyBindingsDialog.FileName $sensitivityDialog.FileName
    }

    $reloadSelectedFiles = {
        if (!$script:ConfigModel) {
            & $selectConfigFiles
            return
        }
        & $loadFiles $script:ConfigModel.SourcePaths[$script:TargetFiles[0]] $script:ConfigModel.SourcePaths[$script:TargetFiles[1]]
    }

    $showWeapon = {
        $weapon = $weaponList.SelectedItem.Name
        if (!$weapon -or !$script:ConfigModel) { return }
           Fill-WeaponFields $script:ConfigModel $inputBoxes $weapon $selectedLbl $selectedWeaponLbl $mouseModelList.SelectedItem.Value
    }

    $saveChanges = {
        try {
            if (!$script:ConfigModel) { return }
            $weapon = if ($weaponList.SelectedItem) { $weaponList.SelectedItem.Name } else { $null }
            if (!$pressList.SelectedItem) { throw '触发方式：请选择一个选项。' }
            if (!$modeSwitchList.SelectedItem) { throw '灵敏度切换键：请选择一个选项。' }

            # Update in-memory content for all files
            foreach ($file in $script:TargetFiles) {
                $fileData = $script:ConfigModel.Files[$file]
                $newContent = $fileData.Content

                if ($file -eq $script:TargetFiles[0]) {
                    $newContent = Set-LuaValue $newContent 'press' $pressList.SelectedItem.Num
                    $newContent = Set-LuaStringValue $newContent 'modeswitch' $modeSwitchList.SelectedItem.Value
                }

                if ($weapon) {
                    foreach ($field in ($script:FieldDefs | Where-Object File -eq $file)) {
                        $key = "$file|$($field.VarSuffix)"
                        $ctrl = $inputBoxes[$key]
                        if ($ctrl.IsEnabled) {
                        if ($field.Type -eq 'Combo') {
                            if (!$ctrl.SelectedItem) { throw "$($field.Label)：请选择一个按键。" }
                            $checked = $ctrl.SelectedItem.Num
                            } else {
                                $checked = ConvertTo-DecimalValue $ctrl.Text $field.SavePattern "$($field.Label)：$($field.HelpText)"
                            }
                            $varName = "${weapon}_$($field.VarSuffix)"
                            $newContent = Set-LuaValue $newContent $varName $checked
                        }
                    }

                    # ---- Reset conflicting keys on other weapons ----
                    if ($file -eq $script:TargetFiles[0]) {
                        $mySuffixValues = @{}
                        foreach ($field in ($script:FieldDefs | Where-Object File -eq $file)) {
                            $key = "$file|$($field.VarSuffix)"
                            $ctrl = $inputBoxes[$key]
                            if ($ctrl.IsEnabled) {
                                $val = if ($field.Type -eq 'Combo') { $ctrl.SelectedItem.Num } else { $ctrl.Text }
                                if ($val -ne '0') { $mySuffixValues[$field.VarSuffix] = $val }
                            }
                        }
                        if ($mySuffixValues.Count) {
                            $allAssigns = Get-LuaAssignments $newContent
                            foreach ($assign in $allAssigns) {
                                if ($assign.Name -match "^(?<w>[A-Za-z0-9]+)_(?<s>.+)$" -and
                                    $Matches.w -ne $weapon -and
                                    $assign.Value -ne '0' -and
                                    $mySuffixValues.ContainsKey($Matches.s) -and
                                    $mySuffixValues[$Matches.s] -eq $assign.Value) {
                                    $newContent = Set-LuaValue $newContent $assign.Name '0'
                                }
                            }
                        }
                    }
                }
                $fileData.Content = $newContent
            }

            # Write all files atomically
            foreach ($file in $script:TargetFiles) {
                $fileData = $script:ConfigModel.Files[$file]
                Save-LuaFile $fileData.Path $fileData.Content $fileData.Encoding
            }

            $selectedWeapon = $weapon
            & $refreshWeaponList $selectedWeapon
            $saveBtn.Content = '应用成功'
            if ($script:saveResetTimer) { $script:saveResetTimer.Stop() }
            $script:saveResetTimer = New-Object Windows.Threading.DispatcherTimer
            $script:saveResetTimer.Interval = [TimeSpan]::FromSeconds(1.5)
            $script:saveResetTimer.Tag = $saveBtn
            $script:saveResetTimer.Add_Tick({
                $this.Tag.Content = '应用'; $this.Stop()
            })
            $script:saveResetTimer.Start()
        } catch {
            $saveBtn.Content = '应用'
            [System.Windows.MessageBox]::Show($_.Exception.Message, '保存失败', 'OK', 'Warning')
        }
    }

    $refreshMouseProfile = {
        if ($weaponList.SelectedItem -and $script:ConfigModel) { & $showWeapon }
    }

    # Wire events
    $minimizeBtn.Add_Click({
        $window.WindowState = [Windows.WindowState]::Minimized
    })
    $closeBtn.Add_Click({
        $window.Close()
    })

    $refreshBtn.Add_Click($reloadSelectedFiles)
    $browseBtn.Add_Click($selectConfigFiles)
    $mouseModelList.Add_SelectionChanged($refreshMouseProfile)
    $weaponList.Add_SelectionChanged($showWeapon)
    $saveBtn.Add_Click($saveChanges)

    [void]$window.ShowDialog()
}

# ---- Entry point -------------------------------------------------------
if ($MyInvocation.InvocationName -ne '.') { Start-Gui }
