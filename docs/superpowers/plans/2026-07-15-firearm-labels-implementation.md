# Firearm Labels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace user-facing weapon terminology with firearm terminology and remove the redundant right-side setup title.

**Architecture:** Make display-string-only edits to the existing WPF XAML and the two runtime updates to the selected-label text. Internal PowerShell symbols and all configuration logic remain unchanged.

**Tech Stack:** PowerShell, WPF/XAML

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Left title: `武器` becomes `枪械`.
- Remove the right-side `武器设置` text block.
- Right selected title: `武器：<名称>` becomes `枪械：<名称>`.
- Empty selection text: `请选择武器` becomes `请选择枪械`.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Update the firearm labels

**Files:**
- Modify: `AMacQGuiEditor.ps1:313`, `AMacQGuiEditor.ps1:473`, `AMacQGuiEditor.ps1:494-495`, and `AMacQGuiEditor.ps1:595`
- Test: Source-content assertion and PowerShell parser check

**Interfaces:**
- Consumes: Existing `SelectedLabel` control and `Fill-WeaponFields` function.
- Produces: Only revised user-visible text; no function signatures or data structures change.

- [ ] **Step 1: Write a failing label assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -match 'Text=\"枪械\"' -and $source -match '枪械：\$Weapon' -and $source -match '请选择枪械' -and $source -notmatch 'Text=\"武器设置\"') { 'Firearm label source check passed' } else { throw 'Firearm labels have not been fully updated.' }"
```

Expected: FAIL with `Firearm labels have not been fully updated.`

- [ ] **Step 2: Apply the four display-string changes**

Make the following exact replacements:

```powershell
$SelectedLabel.Text = "枪械：$Weapon"
```

```xml
<TextBlock Text="枪械" FontSize="11" FontWeight="SemiBold" Foreground="#6E6E73"
           Margin="6,0,0,7"/>
```

Delete this XAML line:

```xml
<TextBlock Text="武器设置" FontSize="13" Foreground="#6E6E73" Margin="0,0,0,5"/>
```

Replace both initial/reset selected-label strings with:

```powershell
'请选择枪械'
```

- [ ] **Step 3: Run the label assertion again**

Run the command from Step 1.

Expected: `Firearm label source check passed`.

- [ ] **Step 4: Parse-check the PowerShell script**

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed`.

- [ ] **Step 5: Manually verify display labels**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify:

1. Left sidebar list title reads `枪械`.
2. `武器设置` is absent from the right header.
3. Selecting MP5 displays `枪械：MP5`.
4. When no item is selected, the text reads `请选择枪械`.

Close the window after testing.

- [ ] **Step 6: Record completion without a commit**

Report the modified script and verification results. Do not run Git commands because this workspace is not a Git repository.

## Self-Review

- **Spec coverage:** All four confirmed display changes are implemented in the sole task.
- **Placeholder scan:** Every text replacement and verification command is explicit.
- **Type consistency:** The existing `SelectedLabel` remains unchanged; only its assigned string values change.
