# ComboBox Vertical Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Vertically center the selected text and dropdown options in every 30-pixel ComboBox.

**Architecture:** Add one shared `ComboBoxItem` style to the embedded XAML resources and apply it to XAML-defined controls. Apply the same style and `VerticalContentAlignment` to ComboBoxes constructed in PowerShell, retaining all existing item data and selection behavior.

**Tech Stack:** PowerShell, WPF/XAML

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Apply vertical centering to mouse model, global trigger, global mode-switch, and firearm key-binding ComboBoxes.
- Do not alter control height, font size, colors, borders, option values, selection behavior, or Lua read/write behavior.
- Do not create a complete ComboBox template.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Add and apply the shared ComboBox item alignment style

**Files:**
- Modify: `AMacQGuiEditor.ps1:281-282`, `AMacQGuiEditor.ps1:356-426`, `AMacQGuiEditor.ps1:462-464`, and `AMacQGuiEditor.ps1:510-517`
- Test: Source-structure assertion, PowerShell parser check, and manual WPF verification

**Interfaces:**
- Consumes: Existing `DisplayMemberPath='Text'` values and all existing ComboBox item object shapes.
- Produces: Resource key `CenteredComboBoxItem` and all target ComboBoxes with `VerticalContentAlignment="Center"`.

- [ ] **Step 1: Write a failing source-structure assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -match 'x:Key=\"CenteredComboBoxItem\"' -and $source -match 'VerticalContentAlignment=\"Center\"' -and $source -match 'ItemContainerStyle=\"\{StaticResource CenteredComboBoxItem\}\"') { 'ComboBox alignment source check passed' } else { throw 'Centered ComboBox styles are missing.' }"
```

Expected: FAIL with `Centered ComboBox styles are missing.`

- [ ] **Step 2: Add the shared item-container style in `Window.Resources`**

Add this before the existing `SidebarButton` style:

```xml
<Style x:Key="CenteredComboBoxItem" TargetType="ComboBoxItem">
  <Setter Property="VerticalContentAlignment" Value="Center"/>
  <Setter Property="Padding" Value="5,3"/>
</Style>
```

- [ ] **Step 3: Apply the style and selected-content alignment to XAML ComboBoxes**

Add these attributes to `MouseModelList`, `PressList`, and `ModeSwitchList`:

```xml
VerticalContentAlignment="Center"
ItemContainerStyle="{StaticResource CenteredComboBoxItem}"
```

Keep all existing attributes and their values unchanged.

- [ ] **Step 4: Apply the same alignment to runtime firearm key ComboBoxes**

In the `$field.Type -eq 'Combo'` branch of `Build-FieldCards`, replace:

```powershell
$ctrl.DisplayMemberPath = 'Text'; $ctrl.Padding = '5,1'
```

with:

```powershell
$ctrl.DisplayMemberPath = 'Text'
$ctrl.VerticalContentAlignment = 'Center'
$ctrl.Padding = '5,0'
$ctrl.ItemContainerStyle = $FieldCardsGrid.FindResource('CenteredComboBoxItem')
```

- [ ] **Step 5: Run the source-structure assertion again**

Run the command from Step 1.

Expected: `ComboBox alignment source check passed`.

- [ ] **Step 6: Parse-check the PowerShell script**

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed`.

- [ ] **Step 7: Manually verify every ComboBox category**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify:

1. Mouse model selected text and expanded options are vertically centered.
2. Global trigger and mode-switch selected text and expanded options are vertically centered.
3. Firearm key-binding selected text and expanded options are vertically centered.
4. All options remain selectable and saving still works.

Close the window after testing.

- [ ] **Step 8: Record completion without a commit**

Report the modified script and verification results. Do not run Git commands because this workspace is not a Git repository.

## Self-Review

- **Spec coverage:** One shared option style covers expanded choices; `VerticalContentAlignment` covers collapsed selected text across all required ComboBoxes.
- **Placeholder scan:** Resource key, property values, target controls, and verification steps are explicit.
- **Type consistency:** `FindResource` returns the WPF `Style` applied to the runtime ComboBox `ItemContainerStyle`; existing item objects retain their `Text` display property.
