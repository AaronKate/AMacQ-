# Sidebar Adaptive Weapon List Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the unused folder section from the sidebar and make the weapon list fill the remaining vertical space.

**Architecture:** Modify the embedded WPF XAML in the existing single PowerShell script. The sidebar grid becomes title toolbar, mouse selector, and star-sized weapon region; the weapon list is no longer given a fixed height in PowerShell.

**Tech Stack:** PowerShell, WPF/XAML, .NET PresentationFramework

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Retain the title-row “刷新” and “浏览...” actions and their existing event handlers.
- Do not display “配置”, “配置文件夹”, a loaded folder path, or a missing-folder path message.
- Keep weapon selection, list scrolling, configuration loading, and saving behavior unchanged.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Remove the folder row and make the weapon list stretch

**Files:**
- Modify: `AMacQGuiEditor.ps1:381-423` and `AMacQGuiEditor.ps1:475-477`
- Test: Source-structure assertion, PowerShell parser check, and manual WPF UI verification

**Interfaces:**
- Consumes: Existing WPF control name `WeaponList` and its existing scroll-bar configuration.
- Produces: A `WeaponList` placed in a star-sized sidebar row without a fixed `Height` value.

- [ ] **Step 1: Write a failing source-structure assertion**

Run this check before changing the current sidebar implementation:

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath '.\AMacQGuiEditor.ps1'; if ($source -notmatch 'Text=\"配置文件夹\"' -and $source -notmatch '\$weaponList.Height\s*=' -and $source -match '<StackPanel Grid.Row=\"2\">' -and $source -match '<RowDefinition Height=\"\*\"/>') { 'Adaptive weapon sidebar source check passed' } else { throw 'Sidebar still contains the folder section, a fixed weapon list height, or lacks the star-sized weapon row.' }"
```

Expected: FAIL with `Sidebar still contains the folder section, a fixed weapon list height, or lacks the star-sized weapon row.`

- [ ] **Step 2: Update the sidebar row definitions and remove the folder section**

Replace the four row definitions with three rows:

```xml
<Grid.RowDefinitions>
  <RowDefinition Height="Auto"/>
  <RowDefinition Height="Auto"/>
  <RowDefinition Height="*"/>
</Grid.RowDefinitions>
```

Delete the complete `StackPanel Grid.Row="2"` block containing `Text="配置文件夹"`. Change the weapon block from `Grid.Row="3"` to the following stretchable layout:

```xml
<StackPanel Grid.Row="2">
  <TextBlock Text="武器" FontSize="11" FontWeight="SemiBold" Foreground="#6E6E73"
             Margin="6,0,0,7"/>
  <Border BorderBrush="#D1D1D6" BorderThickness="1" CornerRadius="8" Background="#FFFFFF"
          VerticalAlignment="Stretch">
    <ListBox Name="WeaponList" BorderThickness="0" Background="Transparent"
             FontSize="14" Foreground="#1D1D1F"/>
  </Border>
</StackPanel>
```

- [ ] **Step 3: Remove the fixed height assignment**

Delete only this existing statement; retain the scroll-bar property assignment immediately after it:

```powershell
$weaponList.Height = 300
```

- [ ] **Step 4: Run the source-structure assertion again**

Run:

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath '.\AMacQGuiEditor.ps1'; if ($source -notmatch 'Text=\"配置文件夹\"' -and $source -notmatch '\$weaponList.Height\s*=' -and $source -match '<StackPanel Grid.Row=\"2\">' -and $source -match '<RowDefinition Height=\"\*\"/>') { 'Adaptive weapon sidebar source check passed' } else { throw 'Sidebar still contains the folder section, a fixed weapon list height, or lacks the star-sized weapon row.' }"
```

Expected: `Adaptive weapon sidebar source check passed`.

- [ ] **Step 5: Parse-check the PowerShell script**

Run:

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath '.\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed` with no parser exception.

- [ ] **Step 6: Manually verify the WPF interface**

Run using an absolute path so the GUI process does not depend on the shell working directory:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify:

1. No “配置文件夹” label or blank row appears between the mouse selector and weapon section.
2. The weapon section begins directly below the mouse selector.
3. The weapon list frame expands and contracts as the window height changes.
4. The list remains scrollable.
5. The title-row “刷新” and “浏览...” controls still work.

Close the window after testing.

- [ ] **Step 7: Record completion without a commit**

This workspace has no Git repository. Report the modified script and verification outputs; do not run Git commands.

## Self-Review

- **Spec coverage:** Task 1 removes the folder section and empty row, shifts the weapon area to the star-sized final row, removes the fixed height, and preserves actions and scroll behavior.
- **Placeholder scan:** All code edits and verification commands are explicit.
- **Type consistency:** `WeaponList` remains the existing named `ListBox`; the existing PowerShell code continues to find it and configure its vertical scroll bar.
