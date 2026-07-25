# Logitech Mouse Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Logitech gaming mouse model selection that filters the macro-key dropdowns to safe, model-specific side buttons while preserving legacy Lua values until the user changes them.

**Architecture:** Keep all mouse-profile metadata in `$script:MouseProfiles` at the top of `AMacQGuiEditor.ps1`. Add a model `ComboBox` to the existing header. New helper functions create dropdown items for a profile, preserve an unmatched existing Lua value as a disabled/display-only `当前配置(n)` item, and repopulate the three `sorinkg.lua` key controls on model change. The existing save path continues to write `SelectedItem.Num` to Lua.

**Tech Stack:** Windows PowerShell 5.1, WPF / PresentationFramework, embedded XAML, Lua text configuration files.

## Global Constraints

- Keep the project as a zero-dependency PowerShell + WPF desktop tool.
- Keep `AMacQGuiEditor.ps1` encoded as UTF-8 with BOM for Windows PowerShell 5.1 Chinese text support.
- Do not include left/right click, wheel middle/direction buttons, DPI buttons, sniper buttons, or other default mouse-function buttons.
- Do not automatically write Lua when the selected mouse model changes.
- Preserve existing Lua numbers not offered by the selected model as `当前配置(n)` until the user changes that field.
- Save only the numeric `Num` value for a selected macro key.

---

### Task 1: Define profile metadata and test profile/key-item helpers

**Files:**
- Modify: `AMacQGuiEditor.ps1:10-21`
- Modify: `AMacQGuiEditor.ps1:42-60`

**Interfaces:**
- Produces `$script:MouseProfiles`, an ordered mapping of model name to `@(@{ Text = [string]; Num = [string] })`.
- Produces `Get-MouseProfileBindings([string]$ModelName)`, returning the profile bindings.
- Produces `New-KeyBindingItems([string]$ModelName, [string]$CurrentValue)`, returning display objects with `Text`, `Num`, and `IsLegacy`.

- [ ] **Step 1: Write a failing helper test script in the PowerShell console**

```powershell
. .\AMacQGuiEditor.ps1

$gpw = Get-MouseProfileBindings 'G Pro Wireless（GPW）'
if (($gpw.Num -join ',') -ne '4,5,7,8') { throw "GPW mapping incorrect: $($gpw.Num -join ',')" }

$gpx = Get-MouseProfileBindings 'G Pro X Superlight（GPX）'
if (($gpx.Num -join ',') -ne '4,5') { throw "GPX mapping incorrect: $($gpx.Num -join ',')" }

$legacy = New-KeyBindingItems 'G102' '8'
if ($legacy[-1].Text -ne '当前配置(8)' -or !$legacy[-1].IsLegacy) { throw 'Legacy value was not preserved.' }

$normal = New-KeyBindingItems 'G102' '4'
if (($normal.Num -join ',') -ne '4,5') { throw 'Normal profile items are incorrect.' }
```

- [ ] **Step 2: Run the helper test and verify it fails**

Run:

```powershell
powershell.exe -NoProfile -Command ". .\AMacQGuiEditor.ps1; Get-MouseProfileBindings 'G Pro Wireless（GPW）'"
```

Expected: failure because `Get-MouseProfileBindings` is not defined.

- [ ] **Step 3: Add the profile table and helper functions**

Replace the global `$script:KeyBindings` table with this profile table and add the two functions immediately after `$script:FieldDefs`:

```powershell
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
    @($script:MouseProfiles[$ModelName] | ForEach-Object {
        [pscustomobject]@{ Text = $_.Text; Num = $_.Num; IsLegacy = $false }
    })
}

function New-KeyBindingItems {
    param([string]$ModelName, [string]$CurrentValue)
    $items = @(Get-MouseProfileBindings $ModelName)
    if ($CurrentValue -and !($items.Num -contains $CurrentValue)) {
        $items += [pscustomobject]@{
            Text = "当前配置($CurrentValue)"
            Num = $CurrentValue
            IsLegacy = $true
        }
    }
    $items
}
```

- [ ] **Step 4: Run the helper test and verify it passes**

Run the Step 1 script.

Expected: no output and exit code `0`.

- [ ] **Step 5: Validate PowerShell syntax**

Run:

```powershell
powershell.exe -NoProfile -Command '$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"),[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|%{$_.ToString()};exit 1};"PASSED"'
```

Expected: `PASSED`.

- [ ] **Step 6: Commit**

This workspace is not a Git repository. Do not run a commit command.

### Task 2: Add the mouse model selector and profile-aware key controls

**Files:**
- Modify: `AMacQGuiEditor.ps1:178-225`
- Modify: `AMacQGuiEditor.ps1:343-358`
- Modify: `AMacQGuiEditor.ps1:363-430`

**Interfaces:**
- Consumes `New-KeyBindingItems` from Task 1.
- Produces `Set-KeyComboItems($ComboBox, [string]$ModelName, [string]$CurrentValue)`, which refreshes a key ComboBox and selects its matching numeric value.
- Produces a WPF model ComboBox named `MouseModelList`.

- [ ] **Step 1: Add a model dropdown to the header XAML**

Insert this row between the existing `FolderPath` DockPanel and the closing header `StackPanel` tag:

```xml
<StackPanel Orientation="Horizontal" Margin="0,10,0,0">
  <TextBlock Text="鼠标型号" VerticalAlignment="Center" FontSize="13"
             Foreground="#8E8E93" Margin="0,0,10,0"/>
  <ComboBox Name="MouseModelList" Width="220" Height="30"
            FontSize="13" Foreground="#1D1D1F"/>
</StackPanel>
```

- [ ] **Step 2: Replace eager fixed binding population in `Build-FieldCards`**

For `if ($field.Type -eq 'Combo')`, remove the loop over `$script:KeyBindings`. Leave a blank ComboBox with `DisplayMemberPath = 'Text'`; the profile selection handler will populate it:

```powershell
if ($field.Type -eq 'Combo') {
    $ctrl = New-Object Windows.Controls.ComboBox
    $ctrl.Height = 32; $ctrl.MinWidth = 50
    $ctrl.Background = $bc.ConvertFromString('#F2F2F7')
    $ctrl.BorderBrush = $bc.ConvertFromString('Transparent')
    $ctrl.BorderThickness = '0'
    $ctrl.FontSize = 14
    $ctrl.Foreground = $bc.ConvertFromString('#1D1D1F')
    $ctrl.Padding = '4,2'
    $ctrl.DisplayMemberPath = 'Text'
}
```

- [ ] **Step 3: Add the controls and the key-combo helper after the existing control lookups**

Add:

```powershell
$mouseModelList = $window.FindName('MouseModelList')

function Set-KeyComboItems {
    param($ComboBox, [string]$ModelName, [string]$CurrentValue)
    $ComboBox.Items.Clear()
    $selectedIndex = -1
    $index = 0
    foreach ($item in (New-KeyBindingItems $ModelName $CurrentValue)) {
        [void]$ComboBox.Items.Add($item)
        if ($item.Num -eq $CurrentValue) { $selectedIndex = $index }
        $index++
    }
    $ComboBox.SelectedIndex = $selectedIndex
}
```

Populate the model selector once after `Build-FieldCards`:

```powershell
$script:MouseProfiles.Keys | ForEach-Object { [void]$mouseModelList.Items.Add($_) }
$mouseModelList.SelectedItem = '通用双侧键鼠标'
```

- [ ] **Step 4: Update `Fill-WeaponFields` to populate model-specific options**

Change its signature to accept `$ModelName`. For each Combo field, replace the existing `for` loop with:

```powershell
Set-KeyComboItems $ctrl $ModelName $val
```

For a missing Combo field, clear its items, set `SelectedIndex = -1`, and disable it:

```powershell
if ($field.Type -eq 'Combo') {
    $ctrl.Items.Clear()
    $ctrl.SelectedIndex = -1
}
$ctrl.IsEnabled = $false
```

- [ ] **Step 5: Pass selected model into weapon rendering and handle model changes**

Change the weapon selection handler to pass the current model:

```powershell
$showWeapon = {
    $weapon = $weaponList.SelectedItem
    if (!$weapon -or !$script:ConfigModel) { return }
    Fill-WeaponFields $script:ConfigModel $inputBoxes $weapon $selectedLbl $mouseModelList.SelectedItem
}
```

Add the following handler before event wiring:

```powershell
$refreshMouseProfile = {
    if ($weaponList.SelectedItem -and $script:ConfigModel) { & $showWeapon }
}
```

Wire it:

```powershell
$mouseModelList.Add_SelectionChanged($refreshMouseProfile)
```

- [ ] **Step 6: Test profile filtering manually**

Run the launcher. Select GPW and choose a weapon with all three macro variables.

Expected:
- the three “按键” ComboBoxes list `4, 5, 7, 8` only;
- select GPX or G102 and they list `4, 5` only;
- no Lua file is modified until the `保存` button is clicked.

- [ ] **Step 7: Validate syntax and commit**

Run the parser command from Task 1, Step 5. Expected: `PASSED`.

This workspace is not a Git repository. Do not run a commit command.

### Task 3: Preserve and save legacy values safely

**Files:**
- Modify: `AMacQGuiEditor.ps1:510-545`

**Interfaces:**
- Consumes ComboBox items created by `Set-KeyComboItems`.
- Ensures saving uses only `.SelectedItem.Num` and reports an unset key selection clearly.

- [ ] **Step 1: Write failing save-value checks in the PowerShell console**

```powershell
$item = [pscustomobject]@{ Text='当前配置(9)'; Num='9'; IsLegacy=$true }
if ($item.Num -ne '9') { throw 'Legacy key would not be persisted as its number.' }

$item = [pscustomobject]@{ Text='右侧前进键(8)'; Num='8'; IsLegacy=$false }
if ($item.Num -ne '8') { throw 'GPW side key would not be persisted as 8.' }
```

- [ ] **Step 2: Add a null selection guard to the Combo save path**

In the `if ($field.Type -eq 'Combo')` branch of `$saveChanges`, use:

```powershell
if (!$ctrl.SelectedItem) {
    throw "$($field.Label)：请选择一个按键。"
}
$checked = $ctrl.SelectedItem.Num
```

Keep the existing TextBox validation branch unchanged.

- [ ] **Step 3: Verify save behavior manually with a temporary configuration copy**

Create a temporary folder containing both required Lua files. Place this in `sorinkg.lua`:

```lua
AK_qq1156777787 = 8
AK_qq1156777787_second = 4
AK_Third = 5
```

Open it through `浏览...`, select model `G102`, and select weapon `AK`.

Expected:
- the first ComboBox displays `当前配置(8)`;
- do not change selections and click save: the file remains `8,4,5`;
- select `G Pro Wireless（GPW）`, change the first field to `右侧前进键(8)`, click save: the first Lua value remains numeric `8`;
- clear a Combo selection if possible and click save: a `保存失败` dialog says which field needs a key selection.

- [ ] **Step 4: Run final non-interactive checks**

Run:

```powershell
powershell.exe -NoProfile -Command '
. .\AMacQGuiEditor.ps1
if ((Get-MouseProfileBindings "G Pro Wireless（GPW）").Num -join "," -ne "4,5,7,8") { throw "GPW profile failed" }
if ((New-KeyBindingItems "G102" "8")[-1].Text -ne "当前配置(8)") { throw "Legacy display failed" }
"PASSED"
'
```

Expected: `PASSED`.

- [ ] **Step 5: Restore UTF-8 BOM and validate syntax**

Run:

```powershell
powershell.exe -NoProfile -Command '$p=Resolve-Path ".\AMacQGuiEditor.ps1";$c=[System.IO.File]::ReadAllText($p,[System.Text.UTF8Encoding]::new($false));[System.IO.File]::WriteAllText($p,$c,[System.Text.UTF8Encoding]::new($true));$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile($p,[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|%{$_.ToString()};exit 1};"PASSED"'
```

Expected: `PASSED`.

- [ ] **Step 6: Commit**

This workspace is not a Git repository. Do not run a commit command.
