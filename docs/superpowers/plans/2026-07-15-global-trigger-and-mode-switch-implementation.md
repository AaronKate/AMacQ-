# Global Trigger and Mode Switch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add right-side global settings for `press` and `modeswitch` in `sorinkg.lua` and save their selected values safely.

**Architecture:** Add static item collections and Lua string-assignment helpers to the existing single PowerShell script. Add a compact global-settings card below the current weapon title, populate it upon successful folder load, and incorporate global writes into the existing atomic save flow independently from any selected weapon.

**Tech Stack:** PowerShell, WPF/XAML, .NET PresentationFramework

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- `press=1` displays as “鼠标左键”; `press=3` displays as “按住右键 + 鼠标左键”.
- `modeswitch` supports only `scrolllock`, `capslock`, and `numlock`, displayed as Scroll Lock, Caps Lock, and Num Lock.
- Place the global settings card below the right-side weapon title and before weapon-specific configuration details.
- Global settings do not change when the selected weapon changes.
- Retain existing atomic writes, encoding preservation, weapon validation, conflict cleanup, and save feedback.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Add global-setting definitions and quoted Lua assignment support

**Files:**
- Modify: `AMacQGuiEditor.ps1:45-84` and `AMacQGuiEditor.ps1:89-123`
- Test: Inline PowerShell helper assertions

**Interfaces:**
- Produces: `$script:PressOptions`, `$script:ModeSwitchOptions`, `Get-LuaStringValue([string]$Content, [string]$VarName)`, and `Set-LuaStringValue([string]$Content, [string]$VarName, [string]$NewValue)`.
- Consumes: Existing plain-text Lua file content and the current `Set-LuaValue` numeric helper pattern.

- [ ] **Step 1: Write a failing helper assertion**

Run this one-off PowerShell command. It intentionally requires helpers not yet present:

```bash
powershell.exe -NoProfile -Command ". 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; $content = 'press=3`nmodeswitch = \"scrolllock\"'; if ((Get-LuaStringValue $content 'modeswitch') -eq 'scrolllock' -and (Set-LuaStringValue $content 'modeswitch' 'numlock') -match 'modeswitch = \"numlock\"') { 'Global Lua helper check passed' } else { throw 'Global Lua helper check failed.' }"
```

Expected: FAIL because `Get-LuaStringValue` and `Set-LuaStringValue` do not exist.

- [ ] **Step 2: Add global option definitions**

After `$script:MouseProfiles`, add:

```powershell
$script:PressOptions = @(
    [pscustomobject]@{ Text='鼠标左键'; Num='1' }
    [pscustomobject]@{ Text='按住右键 + 鼠标左键'; Num='3' }
)

$script:ModeSwitchOptions = @(
    [pscustomobject]@{ Text='Scroll Lock'; Value='scrolllock' }
    [pscustomobject]@{ Text='Caps Lock'; Value='capslock' }
    [pscustomobject]@{ Text='Num Lock'; Value='numlock' }
)
```

- [ ] **Step 3: Add quoted Lua string read/write helpers**

After `Set-LuaValue`, add:

```powershell
function Get-LuaStringValue {
    param([string]$Content, [string]$VarName)
    $escaped = [regex]::Escape($VarName)
    $match = [regex]::Match($Content, "(?m)^\s*$escaped\s*=\s*\"(?<v>[^\"]*)\"")
    if ($match.Success) { return $match.Groups['v'].Value }
    $null
}

function Set-LuaStringValue {
    param([string]$Content, [string]$VarName, [string]$NewValue)
    $escaped = [regex]::Escape($VarName)
    $match = [regex]::Match($Content, "(?m)^(?<l>\s*$escaped\s*=\s*)\"[^\"]*\"")
    if (!$match.Success) { throw "Variable not found in content: $VarName" }
    $Content.Substring(0, $match.Index) + $match.Groups['l'].Value +
        '"' + $NewValue + '"' + $Content.Substring($match.Index + $match.Length)
}
```

- [ ] **Step 4: Run the helper assertion again**

Run the same command from Step 1.

Expected: `Global Lua helper check passed`.

### Task 2: Add the right-side global settings card and load its values

**Files:**
- Modify: `AMacQGuiEditor.ps1:435-455`, `AMacQGuiEditor.ps1:465-475`, and `AMacQGuiEditor.ps1:532-555`
- Test: Source-structure assertion and manual WPF verification

**Interfaces:**
- Consumes: `$script:PressOptions`, `$script:ModeSwitchOptions`, `Get-LuaStringValue`, and `$script:ConfigModel.Files['sorinkg.lua'].Content`.
- Produces: Named `PressList` and `ModeSwitchList` combo boxes whose selected items expose `Num` and `Value` respectively.

- [ ] **Step 1: Write a failing source-structure assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -match 'Name=\"PressList\"' -and $source -match 'Name=\"ModeSwitchList\"' -and $source -match 'Text=\"全局设置\"') { 'Global settings UI source check passed' } else { throw 'Global settings controls are missing.' }"
```

Expected: FAIL with `Global settings controls are missing.`

- [ ] **Step 2: Add the global settings card below the weapon title**

Add this stack panel after the existing title border and before the `ScrollViewer`. Put it in the detail grid’s second row, then change the `ScrollViewer` from `Grid.Row="1"` to `Grid.Row="2"`, and add a new `Auto` row between the title and scrolling content.

```xml
<Border Grid.Row="1" BorderBrush="#E5E5EA" BorderThickness="0,0,0,1" Padding="32,14">
  <StackPanel MaxWidth="760" HorizontalAlignment="Left">
    <TextBlock Text="全局设置" FontSize="13" FontWeight="SemiBold" Foreground="#6E6E73"
               Margin="0,0,0,10"/>
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="16"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>
      <StackPanel>
        <TextBlock Text="触发方式" FontSize="12" Foreground="#3C3C43" Margin="0,0,0,5"/>
        <ComboBox Name="PressList" Height="30" FontSize="13" DisplayMemberPath="Text"
                  Foreground="#1D1D1F" Background="#F5F5F7" BorderBrush="#C7C7CC" BorderThickness="1"/>
      </StackPanel>
      <StackPanel Grid.Column="2">
        <TextBlock Text="灵敏度切换键" FontSize="12" Foreground="#3C3C43" Margin="0,0,0,5"/>
        <ComboBox Name="ModeSwitchList" Height="30" FontSize="13" DisplayMemberPath="Text"
                  Foreground="#1D1D1F" Background="#F5F5F7" BorderBrush="#C7C7CC" BorderThickness="1"/>
      </StackPanel>
    </Grid>
  </StackPanel>
</Border>
```

The detail grid rows must become:

```xml
<Grid.RowDefinitions>
  <RowDefinition Height="Auto"/>
  <RowDefinition Height="Auto"/>
  <RowDefinition Height="*"/>
  <RowDefinition Height="Auto"/>
</Grid.RowDefinitions>
```

Move the save footer from `Grid.Row="2"` to `Grid.Row="3"`.

- [ ] **Step 3: Find and initialize the global controls**

After the existing `$mouseModelList` lookup, add:

```powershell
$pressList      = $window.FindName('PressList')
$modeSwitchList = $window.FindName('ModeSwitchList')

$script:PressOptions | ForEach-Object { [void]$pressList.Items.Add($_) }
$script:ModeSwitchOptions | ForEach-Object { [void]$modeSwitchList.Items.Add($_) }
```

- [ ] **Step 4: Populate global settings when a folder loads**

In `$loadFolder`, immediately after `$script:ConfigModel = Read-AMacQConfig $path`, add:

```powershell
$globalContent = $script:ConfigModel.Files['sorinkg.lua'].Content
$pressValue = Get-LuaAssignments $globalContent | Where-Object Name -eq 'press' | Select-Object -First 1 -ExpandProperty Value
$modeSwitchValue = Get-LuaStringValue $globalContent 'modeswitch'
$pressList.SelectedItem = @($pressList.Items | Where-Object Num -eq $pressValue)[0]
$modeSwitchList.SelectedItem = @($modeSwitchList.Items | Where-Object Value -eq $modeSwitchValue)[0]
```

In the `$loadFolder` `catch` block, clear both selections:

```powershell
$pressList.SelectedIndex = -1
$modeSwitchList.SelectedIndex = -1
```

- [ ] **Step 5: Run the source-structure assertion again**

Run the command from Step 1.

Expected: `Global settings UI source check passed`.

### Task 3: Validate and save global settings independently of weapon selection

**Files:**
- Modify: `AMacQGuiEditor.ps1:580-650`
- Test: One-off save-content helper assertion, PowerShell parser check, and manual WPF verification

**Interfaces:**
- Consumes: `$pressList.SelectedItem.Num`, `$modeSwitchList.SelectedItem.Value`, and `Set-LuaValue`/`Set-LuaStringValue`.
- Produces: Updated in-memory `sorinkg.lua` content that includes the selected `press` and `modeswitch` values before existing atomic writes.

- [ ] **Step 1: Write a failing source-structure assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -match 'Set-LuaValue \$newContent \'press\'' -and $source -match 'Set-LuaStringValue \$newContent \'modeswitch\'') { 'Global save source check passed' } else { throw 'Global settings are not saved.' }"
```

Expected: FAIL with `Global settings are not saved.`

- [ ] **Step 2: Validate and apply selected global values at the start of `$saveChanges`**

Replace this early return:

```powershell
if (!$weapon -or !$script:ConfigModel) { return }
```

with validation that only requires a loaded configuration:

```powershell
if (!$script:ConfigModel) { return }
if (!$pressList.SelectedItem) { throw '触发方式：请选择一个选项。' }
if (!$modeSwitchList.SelectedItem) { throw '灵敏度切换键：请选择一个选项。' }
```

Inside the per-file loop, immediately after `$newContent = $fileData.Content`, add this condition:

```powershell
if ($file -eq 'sorinkg.lua') {
    $newContent = Set-LuaValue $newContent 'press' $pressList.SelectedItem.Num
    $newContent = Set-LuaStringValue $newContent 'modeswitch' $modeSwitchList.SelectedItem.Value
}
```

Wrap all existing loops that interpolate `${weapon}_...`, including the main `$script:FieldDefs` update loop and the conflicting-key reset logic, in `if ($weapon) { ... }`. Preserve their exact bodies and retain `$fileData.Content = $newContent` after the conditional blocks.

- [ ] **Step 3: Run the global save source-structure assertion again**

Run the command from Step 1.

Expected: `Global save source check passed`.

- [ ] **Step 4: Parse-check the completed script**

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed`.

- [ ] **Step 5: Manually verify load and save behavior**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify all of the following with a copy of a valid configuration folder:

1. `press=3` selects “按住右键 + 鼠标左键”; changing to “鼠标左键” saves `press=1`.
2. Each `modeswitch` option displays the expected name and saves the expected lowercase quoted value.
3. Changing a global item, choosing a weapon, and saving preserves the existing weapon edits.
4. With no selected weapon, valid global settings still save.
5. If either setting is unselected, saving displays the specified validation message and does not write files.
6. Existing save success feedback still appears.

Close the window after testing.

- [ ] **Step 6: Record completion without a commit**

Report the changed script and executed verification results. This workspace does not have a Git repository; do not run Git commands.

## Self-Review

- **Spec coverage:** Task 1 supplies explicit mappings and quoted string support; Task 2 adds and populates the card in the specified right-side location; Task 3 validates and saves global values independently from weapon selection without changing atomic writes.
- **Placeholder scan:** All commands, values, field names, and required edits are explicit.
- **Type consistency:** `PressList` contains objects with `Text`/`Num`; `ModeSwitchList` contains objects with `Text`/`Value`; their referenced properties match load and save code.
