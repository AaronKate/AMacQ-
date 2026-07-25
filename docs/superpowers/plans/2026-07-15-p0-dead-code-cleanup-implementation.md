# P0 Dead Code Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove verified unused and duplicate PowerShell UI initialization code without changing behavior.

**Architecture:** Make narrow deletions in `AMacQGuiEditor.ps1`. XAML remains the source of truth for initial window and control presentation values, while the one runtime foreground override that differs from XAML remains intact.

**Tech Stack:** PowerShell, WPF/XAML

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Do not alter Lua parsing, atomic writes, encoding preservation, global settings, weapon selection, or saving behavior.
- Retain `$selectedLbl.Foreground = $bc.ConvertFromString('#007AFF')` because it intentionally differs from the XAML default.
- Retain the `WeaponListItem` WPF template and its inactive selection behavior.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Delete verified unused and redundant initialization code

**Files:**
- Modify: `AMacQGuiEditor.ps1:7`, `AMacQGuiEditor.ps1:543`, and `AMacQGuiEditor.ps1:569-579`
- Test: Source-content assertion, PowerShell parser check, and WPF launch verification

**Interfaces:**
- Consumes: Existing XAML properties for window height, title, weapon list, and save button.
- Produces: Identical UI defaults with no unused suffix constant, duplicate height assignment, or redundant BrushConverter setup.

- [ ] **Step 1: Write a failing cleanup assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -notmatch '\$script:WeaponVarSuffix' -and $source -notmatch '\$window.Height\s*=\s*600' -and $source -notmatch '\$weaponList\.Background\s*=' -and $source -notmatch '\$saveBtn\.Background\s*=' -and $source -notmatch 'BrushConverter') { 'P0 cleanup source check passed' } else { throw 'P0 redundant code is still present.' }"
```

Expected: FAIL with `P0 redundant code is still present.`

- [ ] **Step 2: Remove the unused suffix constant**

Delete this declaration:

```powershell
$script:WeaponVarSuffix = 'qq1156777787'
```

- [ ] **Step 3: Remove duplicate height and redundant theme assignments**

Delete:

```powershell
$window.Height = 600

# Theme
$bc = [Windows.Media.BrushConverter]::new()
$window.Background    = $bc.ConvertFromString('#F5F5F7')
$titleLabel.Foreground = $bc.ConvertFromString('#1D1D1F')

$weaponList.Background   = $bc.ConvertFromString('Transparent')
$weaponList.Foreground   = $bc.ConvertFromString('#1D1D1F')
$weaponList.BorderBrush  = $bc.ConvertFromString('Transparent')

$selectedLbl.Foreground = $bc.ConvertFromString('#007AFF')
$saveBtn.Background     = $bc.ConvertFromString('#007AFF')
```

Replace it with only the intentional runtime override:

```powershell
$selectedLbl.Foreground = [Windows.Media.Brushes]::DodgerBlue
```

This preserves the existing blue selected-firearm title without creating a local `BrushConverter`.

- [ ] **Step 4: Run the source assertion again**

Run the command from Step 1.

Expected: `P0 cleanup source check passed`.

- [ ] **Step 5: Parse-check the script**

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed`.

- [ ] **Step 6: Launch the WPF editor for visual verification**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify:

1. Window starts at the configured 600-pixel height.
2. Selected firearm title remains blue.
3. Weapon list still has its custom stable selected-item styling.
4. Save button remains blue.
5. Loading, selecting a firearm, and global settings display still work.

Close the window after testing.

- [ ] **Step 7: Record completion without a commit**

Report the changed script and verification results. Do not run Git commands because this workspace is not a Git repository.

## Self-Review

- **Spec coverage:** The task removes every P0 item while explicitly retaining the one runtime color override and live selection template.
- **Placeholder scan:** Deletions, replacement code, and verification commands are explicit.
- **Type consistency:** `Brushes.DodgerBlue` is a `Brush`, suitable for `TextBlock.Foreground`; all other removed defaults remain supplied by XAML.
