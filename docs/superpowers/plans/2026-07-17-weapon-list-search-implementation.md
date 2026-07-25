# Weapon List Search Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an immediate, case-insensitive contains search field above the sidebar weapon list.

**Architecture:** Keep the complete weapon set in `$script:ConfigModel.Weapons`; derive the displayed `ListBox` contents from that collection. A small pure helper provides testable matching, while one UI refresh block preserves the selected weapon where possible.

**Tech Stack:** PowerShell, WPF/XAML, Pester.

---

## File structure

- Modify: `AMacQGuiEditor.ps1` — add filtering helper, input, and refresh/event logic.
- Create: `tests/WeaponListSearch.Tests.ps1` — Pester coverage for filtering and search-input presence.

### Task 1: Define and prove filtering behavior

**Files:**

- Create: `tests/WeaponListSearch.Tests.ps1`
- Modify: `AMacQGuiEditor.ps1: after Get-PrimaryWeapons`

- [ ] **Step 1: Write the failing test**

```powershell
. "$PSScriptRoot\..\AMacQGuiEditor.ps1"

Describe 'Get-FilteredWeapons' {
    $weapons = @('AK47', 'M4A1', 'ak12')

    It 'matches a substring without regard to case' {
        @(Get-FilteredWeapons -Weapons $weapons -SearchText 'ak') | Should -Be @('AK47', 'ak12')
    }

    It 'returns every weapon for an empty search term' {
        @(Get-FilteredWeapons -Weapons $weapons -SearchText '') | Should -Be $weapons
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `Invoke-Pester .\tests\WeaponListSearch.Tests.ps1 -Output Detailed`

Expected: the tests fail because `Get-FilteredWeapons` is not recognized.

- [ ] **Step 3: Add the minimal implementation**

```powershell
function Get-FilteredWeapons {
    param([string[]]$Weapons, [string]$SearchText)

    $term = $SearchText.Trim()
    if (!$term) { return @($Weapons) }
    return @($Weapons | Where-Object {
        $_.IndexOf($term, [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `Invoke-Pester .\tests\WeaponListSearch.Tests.ps1 -Output Detailed`

Expected: 2 passed, 0 failed.

- [ ] **Step 5: Commit the task**

Run: `git add AMacQGuiEditor.ps1 tests/WeaponListSearch.Tests.ps1; git commit -m "feat: add weapon search filtering"`

Expected: a commit is created. If the workspace is not a Git repository, record that the commit cannot be performed.

### Task 2: Add the search input and bind filtering

**Files:**

- Modify: `AMacQGuiEditor.ps1: sidebar XAML around WeaponList`
- Modify: `AMacQGuiEditor.ps1: control discovery, $loadFolder, and event wiring`
- Modify: `tests/WeaponListSearch.Tests.ps1`

- [ ] **Step 1: Write the failing integration test**

```powershell
It 'exposes a named search input in the sidebar XAML' {
    $scriptText = Get-Content "$PSScriptRoot\..\AMacQGuiEditor.ps1" -Raw
    $scriptText | Should -Match 'TextBox Name="WeaponSearchBox"'
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `Invoke-Pester .\tests\WeaponListSearch.Tests.ps1 -Output Detailed`

Expected: the new test fails because `WeaponSearchBox` does not exist.

- [ ] **Step 3: Add the minimal UI and event logic**

Replace the weapon grid with three rows: title, input, and list. Add this input:

```xml
<TextBox Name="WeaponSearchBox" Grid.Row="1" Height="30" FontSize="13"
         Foreground="#1D1D1F" Background="#FFFFFF" BorderBrush="#C7C7CC"
         BorderThickness="1" Padding="9,5" Margin="0,0,0,8"
         VerticalContentAlignment="Center" ToolTip="搜索枪械"/>
```

Move the existing `Border` and `WeaponList` to `Grid.Row="2"`. Find the new control and add the refresh block:

```powershell
$weaponSearchBox = $window.FindName('WeaponSearchBox')

$refreshWeaponList = {
    $selectedWeapon = $weaponList.SelectedItem
    $weaponList.Items.Clear()
    if (!$script:ConfigModel) { return }
    $visibleWeapons = Get-FilteredWeapons -Weapons $script:ConfigModel.Weapons -SearchText $weaponSearchBox.Text
    $visibleWeapons | ForEach-Object { [void]$weaponList.Items.Add($_) }
    if ($selectedWeapon -and $visibleWeapons -contains $selectedWeapon) {
        $weaponList.SelectedItem = $selectedWeapon
    } elseif ($weaponList.Items.Count) {
        $weaponList.SelectedIndex = 0
    }
}
```

Set `$weaponSearchBox.Text = ''` in `$loadFolder` after clearing application state; replace direct weapon population there with `& $refreshWeaponList`; wire `$weaponSearchBox.Add_TextChanged({ & $refreshWeaponList })`.

- [ ] **Step 4: Run automated verification**

Run: `Invoke-Pester .\tests\WeaponListSearch.Tests.ps1 -Output Detailed; $tokens=$null; $errors=$null; [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path .\AMacQGuiEditor.ps1), [ref]$tokens, [ref]$errors); if ($errors) { $errors | Format-List; exit 1 }`

Expected: all Pester tests pass and the parser reports no errors.

- [ ] **Step 5: Manually verify WPF behavior**

Run: `.\AMacQGuiEditor.ps1`

Expected: `ak` filters case-insensitively; clearing restores all items; an included selected weapon stays selected; reload clears search; mouse model switch and save still work.

- [ ] **Step 6: Commit the task**

Run: `git add AMacQGuiEditor.ps1 tests/WeaponListSearch.Tests.ps1; git commit -m "feat: add sidebar weapon search"`

Expected: a commit is created. If the workspace is not a Git repository, record that the commit cannot be performed.
