# Weapon List Inactive Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the selected weapon visibly blue with white text even when the weapon list loses focus.

**Architecture:** Replace the runtime `ListBoxItem` style’s reliance on the default WPF control template with a custom template containing a named border and content presenter. Template triggers will explicitly paint default, hover, selected, and pressed states, independent of the system inactive-selection visual state.

**Tech Stack:** PowerShell, WPF, .NET PresentationFramework

## Global Constraints

- Preserve the single-file PowerShell UI and zero external dependencies.
- Selected weapon items use `#007AFF`, white text, and semi-bold weight regardless of focus.
- Unselected items remain transparent with `#1D1D1F` text.
- Hover uses `#E5E5EA`; pressing uses `#006EDC` without overriding selection contrast.
- Preserve existing weapon selection, field loading, list scrolling, and saving behavior.
- This workspace is not a Git repository; do not create commits.

---

### Task 1: Add an explicit ListBoxItem control template

**Files:**
- Modify: `AMacQGuiEditor.ps1:552-576`
- Test: Source-structure assertion, PowerShell parser check, and manual WPF focus verification

**Interfaces:**
- Consumes: Existing `$itemStyle`, `$bc`, and `$weaponList.ItemContainerStyle` assignment.
- Produces: A `Windows.Controls.ControlTemplate` whose visual tree contains the named `Border` `bd` and a `ContentPresenter`.

- [ ] **Step 1: Write a failing source-structure assertion**

```bash
powershell.exe -NoProfile -Command "$source = Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'; if ($source -match 'New-Object Windows.Controls.ControlTemplate \(\[Windows.Controls.ListBoxItem\]\)' -and $source -match 'RegisterName\(''bd''' -and $source -match '#006EDC') { 'Weapon selection template source check passed' } else { throw 'Weapon selection control template is missing.' }"
```

Expected: FAIL with `Weapon selection control template is missing.`

- [ ] **Step 2: Build and apply the explicit visual tree**

After the existing `$itemStyle` property setters, add this code:

```powershell
$template = New-Object Windows.Controls.ControlTemplate ([Windows.Controls.ListBoxItem])
$border = New-Object Windows.FrameworkElementFactory ([Windows.Controls.Border])
$border.Name = 'bd'
$border.SetBinding([Windows.Controls.Border]::BackgroundProperty,
    (New-Object Windows.Data.Binding('Background') -Property @{ RelativeSource = New-Object Windows.Data.RelativeSource ([Windows.Data.RelativeSourceMode]::TemplatedParent) }))
$border.SetValue([Windows.Controls.Border]::CornerRadiusProperty, (New-Object Windows.CornerRadius(6)))

$presenter = New-Object Windows.FrameworkElementFactory ([Windows.Controls.ContentPresenter])
$presenter.SetValue([Windows.Controls.ContentPresenter]::ContentProperty,
    (New-Object Windows.TemplateBindingExtension([Windows.Controls.ContentControl]::ContentProperty)))
$presenter.SetValue([Windows.Controls.ContentPresenter]::ContentTemplateProperty,
    (New-Object Windows.TemplateBindingExtension([Windows.Controls.ContentControl]::ContentTemplateProperty)))
$presenter.SetValue([Windows.Controls.ContentPresenter]::ContentTemplateSelectorProperty,
    (New-Object Windows.TemplateBindingExtension([Windows.Controls.ContentControl]::ContentTemplateSelectorProperty)))
$presenter.SetValue([Windows.Controls.ContentPresenter]::ContentStringFormatProperty,
    (New-Object Windows.TemplateBindingExtension([Windows.Controls.ContentControl]::ContentStringFormatProperty)))
$presenter.SetValue([Windows.Controls.ContentPresenter]::MarginProperty,
    (New-Object Windows.TemplateBindingExtension([Windows.Controls.Control]::PaddingProperty)))
$border.AppendChild($presenter)
$template.VisualTree = $border
$itemStyle.Setters.Add((New-Object Windows.Setter([Windows.Controls.Control]::TemplateProperty, $template)))
```

- [ ] **Step 3: Replace the current DataTrigger with template triggers**

Remove the existing `Windows.DataTrigger` block. Add these `Windows.Trigger` objects to `$template.Triggers`:

```powershell
$hoverTrigger = New-Object Windows.Trigger
$hoverTrigger.Property = [Windows.UIElement]::IsMouseOverProperty
$hoverTrigger.Value = $true
$hoverTrigger.Setters.Add((New-Object Windows.Setter([Windows.Controls.Border]::BackgroundProperty, $bc.ConvertFromString('#E5E5EA'), 'bd')))
$template.Triggers.Add($hoverTrigger)

$selectedTrigger = New-Object Windows.Trigger
$selectedTrigger.Property = [Windows.Controls.ListBoxItem]::IsSelectedProperty
$selectedTrigger.Value = $true
$selectedTrigger.Setters.Add((New-Object Windows.Setter([Windows.Controls.Border]::BackgroundProperty, $bc.ConvertFromString('#007AFF'), 'bd')))
$selectedTrigger.Setters.Add((New-Object Windows.Setter([Windows.Controls.Control]::ForegroundProperty, $bc.ConvertFromString('White'))))
$selectedTrigger.Setters.Add((New-Object Windows.Setter([Windows.Controls.Control]::FontWeightProperty, [System.Windows.FontWeights]::SemiBold)))
$template.Triggers.Add($selectedTrigger)

$pressedTrigger = New-Object Windows.Trigger
$pressedTrigger.Property = [Windows.Controls.Primitives.ButtonBase]::IsPressedProperty
$pressedTrigger.Value = $true
$pressedTrigger.Setters.Add((New-Object Windows.Setter([Windows.Controls.Border]::BackgroundProperty, $bc.ConvertFromString('#006EDC'), 'bd')))
$template.Triggers.Add($pressedTrigger)
```

- [ ] **Step 4: Run the source-structure assertion again**

Run the command from Step 1.

Expected: `Weapon selection template source check passed`.

- [ ] **Step 5: Parse-check the script**

```bash
powershell.exe -NoProfile -Command "[void][scriptblock]::Create((Get-Content -Raw -LiteralPath 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1')); 'PowerShell parse passed'"
```

Expected: `PowerShell parse passed`.

- [ ] **Step 6: Manually verify focus behavior**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\开发项目\AMacQ配置编辑器\AMacQGuiEditor.ps1'
```

Verify:

1. Select a weapon; it appears blue with white semi-bold text.
2. Click either right-side global setting, a weapon field, or the save button; the selected weapon remains blue with white text.
3. Hover an unselected weapon; it becomes light gray with dark text.
4. Hover or press the selected weapon; the text remains white and selection contrast is retained.
5. Weapon selection still updates the right-side fields and the list remains scrollable.

Close the window after testing.

- [ ] **Step 7: Record completion without a commit**

Report the modified script and verification results. Do not run Git commands because this workspace is not a Git repository.

## Self-Review

- **Spec coverage:** The task explicitly replaces the default template, gives all relevant visual states defined colors, and verifies inactive selection plus unaffected list behavior.
- **Placeholder scan:** Code, color values, properties, and test commands are explicit.
- **Type consistency:** The template targets `ListBoxItem`, uses existing `$bc` brushes, and remains assigned through the existing `$weaponList.ItemContainerStyle` path.
