# Sidebar Toolbar Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the AMacQ editor sidebar by moving folder actions beside the application title and removing redundant configuration and path text.

**Architecture:** Keep the WPF interface in the existing embedded XAML within the single PowerShell script. Replace the standalone title with a three-column title row, remove the path display control, and delete only the PowerShell references that serviced that removed display. Existing folder actions and all configuration loading behavior remain intact.

**Tech Stack:** PowerShell, WPF/XAML, .NET PresentationFramework

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Retain the existing `SidebarButton` style and existing event handler names.
- Keep the “配置文件夹” section label, mouse model selector, weapon list, refresh behavior, and browse behavior.
- Do not show “配置”, a loaded folder path, or the missing-folder path message in the sidebar.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Simplify the sidebar XAML and remove obsolete path-display code

**Files:**
- Modify: `AMacQGuiEditor.ps1:380-429` and `AMacQGuiEditor.ps1:470-565`
- Test: Manual WPF UI verification using `AMacQGuiEditor.ps1`

**Interfaces:**
- Consumes: Existing XAML control names `RefreshBtn`, `BrowseBtn`, `MouseModelList`, and `WeaponList`.
- Produces: `RefreshBtn` and `BrowseBtn` remain discoverable through `$window.FindName`; the `FolderPath` control is removed and no longer referenced.

- [ ] **Step 1: Remove the sidebar “配置” label and place action buttons in the title row**

Replace the current standalone `TitleLabel` block and the sidebar row-1 content with this XAML. It preserves the controls used by the existing event bindings while positioning both actions to the right of the title.

```xml
<Grid>
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="*"/>
    <ColumnDefinition Width="Auto"/>
    <ColumnDefinition Width="Auto"/>
  </Grid.ColumnDefinitions>
  <TextBlock Name="TitleLabel" Text="AMacQ"
             FontSize="20" FontWeight="SemiBold" Foreground="#1D1D1F"
             Margin="6,0,0,20"/>
  <Button Name="RefreshBtn" Grid.Column="1" Content="刷新" Style="{StaticResource SidebarButton}"
          Foreground="#007AFF" FontSize="12" Padding="8,6" Margin="0,0,0,14"/>
  <Button Name="BrowseBtn" Grid.Column="2" Content="浏览..." Style="{StaticResource SidebarButton}"
          Foreground="#007AFF" FontSize="12" Padding="8,6" Margin="0,0,0,14"/>
</Grid>

<StackPanel Grid.Row="1">
  <TextBlock Text="鼠标型号" FontSize="12" Foreground="#3C3C43"
             Margin="6,0,0,5"/>
  <ComboBox Name="MouseModelList" Height="30" FontSize="13"
            Foreground="#1D1D1F" Background="#FFFFFF"
            BorderBrush="#C7C7CC" BorderThickness="1" Margin="0,0,0,16"/>
</StackPanel>
```

- [ ] **Step 2: Remove the folder path display and the duplicate action buttons from the folder section**

Replace the current `Grid.Row="2"` stack panel content with only its section label:

```xml
<StackPanel Grid.Row="2" Margin="0,0,0,12">
  <TextBlock Text="配置文件夹" FontSize="11" FontWeight="SemiBold" Foreground="#6E6E73"
             Margin="6,0,0,7"/>
</StackPanel>
```

- [ ] **Step 3: Delete the removed control’s PowerShell references**

Delete these statements and assignments because `FolderPath` no longer exists:

```powershell
$folderPath = $window.FindName('FolderPath')
$folderPath.Foreground  = $bc.ConvertFromString('#86868B')
$folderPath.Text = $path
$folderPath.Text = ''
$folderPath.Text = '未找到 AMacQ 文件夹，请点击 刷新 或 浏览。'
```

Keep the surrounding `try`/`catch`, `$window.Title` updates, and message boxes unchanged.

- [ ] **Step 4: Parse-check the PowerShell script**

Run:

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath '.\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: output includes `PowerShell parse passed` and contains no parser exception.

- [ ] **Step 5: Manually verify the WPF interface and behavior**

Run:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\AMacQGuiEditor.ps1
```

Verify:

1. The sidebar’s first row displays `AMacQ`, then `刷新` and `浏览...` aligned to its right.
2. The sidebar does not display `配置`.
3. The sidebar does not show a configuration folder path or a missing-folder path message.
4. The `配置文件夹` label remains visible.
5. `刷新` loads a detected valid configuration directory when available.
6. `浏览...` opens the chooser and loads a valid folder.
7. On a failed load, the existing message box appears and the window title resets.

Close the window after testing.

- [ ] **Step 6: Record completion without a commit**

This project has no `.git` directory. Do not run `git add` or `git commit`; report the changed script and verification results instead.

## Self-Review

- **Spec coverage:** Task 1 moves both folder actions to the title row, removes the “配置” label and the folder path/missing-folder message, retains the folder section label, and keeps all stated folder behavior intact.
- **Placeholder scan:** No incomplete implementation steps, unspecified test commands, or deferred requirements remain.
- **Type consistency:** The retained WPF names `RefreshBtn`, `BrowseBtn`, `MouseModelList`, and `WeaponList` match the existing PowerShell `FindName` and event-binding calls. `FolderPath` is removed from both XAML and code.
