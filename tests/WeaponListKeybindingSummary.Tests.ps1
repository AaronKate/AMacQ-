$ErrorActionPreference = 'Stop'

$sourcePath = Join-Path $PSScriptRoot '..\AMacQGuiEditor.ps1'
$content = Get-Content -Raw $sourcePath

if ($content -notmatch 'function\s+Get-WeaponListItems') {
    throw 'Get-WeaponListItems is required.'
}

$functionStart = $content.IndexOf('function Get-WeaponListItems')
$functionEnd = $content.IndexOf('function Get-LuaStringValue', $functionStart)
if ($functionStart -lt 0 -or $functionEnd -lt 0) {
    throw 'Get-WeaponListItems must be declared before Get-LuaStringValue.'
}

$prefix = $content.Substring(0, $functionEnd)
Invoke-Expression $prefix

$model = @{
    Files = @{
        KeyBindings = [pscustomobject]@{
            Content = @'
AK_qq1156777787 = 4
AK_qq1156777787_second = 4
AK_Third = 5
M4_qq1156777787 = 0
M4_qq1156777787_second = 3
M4_Third = 0
MP5_qq1156777787 = 0
MP5_qq1156777787_second = 0
MP5_Third = 0
BAD_qq1156777787 = 12
'@
        }
    }
    Weapons = @('AK', 'M4', 'MP5', 'BAD')
}

$items = @(Get-WeaponListItems $model)
if ($items.Count -ne 4) { throw 'The list item builder must return one item per discovered weapon.' }

$separator = [char]0x00B7
$ak = $items | Where-Object Name -eq 'AK'
if ($ak.BindingSummary -ne "4 $separator Alt+4 $separator Ctrl+5" -or !$ak.HasBindingSummary) {
    throw "AK must display normal, Alt, and Ctrl bindings in fixed order. Actual: '$($ak.BindingSummary)'"
}

$m4 = $items | Where-Object Name -eq 'M4'
if ($m4.BindingSummary -ne 'Alt+3' -or !$m4.HasBindingSummary) {
    throw 'A zero-valued normal or Ctrl binding must not appear in the summary.'
}

$mp5 = $items | Where-Object Name -eq 'MP5'
if ($mp5.BindingSummary -ne '' -or $mp5.HasBindingSummary) {
    throw 'A weapon without nonzero bindings must not reserve a summary row.'
}

$bad = $items | Where-Object Name -eq 'BAD'
if ($bad.BindingSummary -ne '' -or $bad.HasBindingSummary) {
    throw 'A binding outside the supported single-digit range must not appear.'
}

if ($content -notmatch '<Style x:Key="WeaponListItem" TargetType="ListBoxItem">' -or
    $content -notmatch 'Text="\{Binding Name\}"' -or
    $content -notmatch 'Text="\{Binding BindingSummary\}"' -or
    $content -notmatch 'TextTrimming="CharacterEllipsis"' -or
    $content -notmatch 'TextWrapping="NoWrap"') {
    throw 'Weapon list items must render a name and single-line ellipsized binding summary.'
}

if ($content -notmatch '<DataTrigger Binding="\{Binding HasBindingSummary\}" Value="False">' -or
    $content -notmatch '<Setter TargetName="BindingSummary" Property="Visibility" Value="Collapsed"/>') {
    throw 'Weapons without bindings must collapse the summary row.'
}

$refreshStart = $content.IndexOf('$refreshWeaponList = {')
$saveStart = $content.IndexOf('$saveChanges = {')
$saveEnd = $content.IndexOf('$refreshMouseProfile = {', $saveStart)
if ($refreshStart -lt 0 -or $saveStart -lt 0 -or $saveEnd -lt 0) {
    throw 'Weapon list refresh and save handlers are required.'
}

$refreshBlock = $content.Substring($refreshStart, $saveStart - $refreshStart)
$saveBlock = $content.Substring($saveStart, $saveEnd - $saveStart)
if (!$refreshBlock.Contains('Get-WeaponListItems $script:ConfigModel')) {
    throw 'The weapon list refresh must rebuild binding summary items.'
}

if (!$saveBlock.Contains('& $refreshWeaponList $selectedWeapon')) {
    throw 'A successful save must refresh summaries while preserving the selected weapon.'
}

$weaponListStyleStart = $content.IndexOf('<Style x:Key="WeaponListItem" TargetType="ListBoxItem">')
$weaponListStyleEnd = $content.IndexOf('</Style>', $weaponListStyleStart) + '</Style>'.Length
$weaponListStyle = $content.Substring($weaponListStyleStart, $weaponListStyleEnd - $weaponListStyleStart)

if ($weaponListStyle -notmatch '<TextBlock x:Name="BindingSummary"[\s\S]*?Foreground="\{StaticResource AccentGradientBrush\}"') {
    throw 'The unselected binding summary must use the shared accent gradient brush.'
}

if ($weaponListStyle -notmatch '<TextBlock Text="\{Binding Name\}"\s+Foreground="\{TemplateBinding Foreground\}"' -or
    $weaponListStyle -match '<TextBlock Text="\{Binding Name\}"[^>]*Foreground="\{StaticResource AccentGradientBrush\}"') {
    throw 'The weapon name must remain solid text rather than use the accent gradient.'
}

$selectedSummarySetters = [regex]::Matches(
    $weaponListStyle,
    '<Setter TargetName="BindingSummary" Property="Foreground" Value="\{DynamicResource AccentForegroundBrush\}"/>'
)
if ($selectedSummarySetters.Count -ne 2) {
    throw 'Selected and inactive-selected summaries must each use AccentForegroundBrush.'
}
