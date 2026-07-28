# 主题颜色变量集中化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将深海极光主题的全部可见颜色集中为命名 `Color` 调色板和语义画刷，使后续换色只需修改 `Window.Resources` 的颜色变量。

**Architecture:** 在现有 WPF `Window.Resources` 的开头建立两层主题系统：第一层为所有具体色值和动画终点的 `Color` 资源；第二层为由调色板组合的语义 `SolidColorBrush`、`LinearGradientBrush` 和 `RadialGradientBrush` 资源。XAML 模板与 PowerShell 运行时字段卡片只消费第二层画刷；背景动画从第一层读取目标 `Color`。

**Tech Stack:** Windows PowerShell 5+、WPF/XAML（PresentationFramework）、ps2exe、现有 PowerShell 静态测试。

## Global Constraints

- 不修改窗口布局、配置读取/写入逻辑、图标资源或 EXE 打包流程。
- 不新增外部依赖、图像资产或运行时服务。
- 所有可见主题颜色必须只在连续的命名 `Color` 调色板资源区出现；布局用的 `Transparent` 除外。
- XAML 控件和模板只能使用命名语义画刷；不得保留 `#...`、`White` 或其他可见颜色字面量。
- `Build-FieldCards` 只能通过 `FindResource` 获取主题画刷，且不得使用 `BrushConverter.ConvertFromString()`。
- `Start-AnimatedBackground` 必须从 `Window.Resources` 的动画目标 `Color` 资源读取颜色；动画周期保持 8 秒。
- 必须保留深海极光背景、无扫描线、蓝青控件状态、青色至靛蓝强调渐变，以及关闭按钮的红色危险悬停反馈。

---

## File Structure

- Modify: `AMacQGuiEditor.ps1` — 定义颜色调色板与语义画刷，替换模板和运行时代码中的颜色字面量，并从资源读取动画终点。
- Modify: `tests/TitleBarLayout.Tests.ps1` — 断言调色板、语义画刷、运行时代码资源读取与生产 UI 区域无颜色字面量。
- Modify: `tests/BuildRelease.Tests.ps1` — 不需要主题断言；仅作为最终打包回归测试运行。
- Create: `docs/superpowers/specs/2026-07-28-deep-ocean-aurora-theme-design.md` — 已批准的集中调色板规格。
- Create: `docs/superpowers/plans/2026-07-28-theme-color-tokens-implementation.md` — 本实施计划。

### Task 1: 固化集中调色板与语义画刷的测试契约

**Files:**
- Modify: `tests/TitleBarLayout.Tests.ps1:5-78`
- Modify: `AMacQGuiEditor.ps1:212-244, 249-332, 407-693`

**Interfaces:**
- Consumes: 已有 `Start-AnimatedBackground([Windows.Window]$Window)` 和 `Build-FieldCards($FieldCardsGrid)`。
- Produces: 统一资源协议：命名 `Color` 资源、语义画刷资源，以及 `$Window.Resources['Animation*Color']` 可返回 `[Windows.Media.Color]` 的动画目标键。

- [ ] **Step 1: 写入会失败的集中调色板断言**

在 `tests/TitleBarLayout.Tests.ps1` 中，紧随已有 `AppBackgroundBrush` 断言后加入：

```powershell
foreach ($requiredColor in @(
    'x:Key="TextPrimaryColor"',
    'x:Key="TextBodyColor"',
    'x:Key="TextSecondaryColor"',
    'x:Key="TextListColor"',
    'x:Key="AccentForegroundColor"',
    'x:Key="BorderControlColor"',
    'x:Key="BorderFocusColor"',
    'x:Key="BorderDividerColor"',
    'x:Key="DangerCloseHoverColor"',
    'x:Key="AnimationAppStartColor"',
    'x:Key="AnimationAppEndColor"',
    'x:Key="AnimationPanelStartColor"',
    'x:Key="AnimationPanelEndColor"',
    'x:Key="AnimationInputStartColor"',
    'x:Key="AnimationInputEndColor"',
    'x:Key="AnimationPopupStartColor"',
    'x:Key="AnimationPopupEndColor"'
)) {
    if ($content -notmatch [regex]::Escape($requiredColor)) {
        throw "The centralized theme palette requires $requiredColor."
    }
}

foreach ($requiredBrush in @(
    'x:Key="PrimaryTextBrush"',
    'x:Key="BodyTextBrush"',
    'x:Key="SecondaryTextBrush"',
    'x:Key="ListItemTextBrush"',
    'x:Key="AccentForegroundBrush"',
    'x:Key="FocusBorderBrush"',
    'x:Key="DividerBrush"',
    'x:Key="PanelOutlineBrush"',
    'x:Key="TitleBarDividerBrush"',
    'x:Key="ScrollTrackBrush"',
    'x:Key="ScrollThumbBrush"',
    'x:Key="ScrollThumbHoverBrush"',
    'x:Key="WindowCloseHoverBrush"'
)) {
    if ($content -notmatch [regex]::Escape($requiredBrush)) {
        throw "The centralized theme brush set requires $requiredBrush."
    }
}

if ($content -notmatch "\$Window\.Resources\['AnimationAppStartColor'\]" -or
    $content -notmatch "\$Window\.Resources\['AnimationPopupEndColor'\]") {
    throw 'The background animation must obtain its target colors from named resources.'
}
```

- [ ] **Step 2: 运行测试并确认它因缺失调色板资源失败**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\TitleBarLayout.Tests.ps1
```

预期：退出码非零，并包含 `The centralized theme palette requires`；现有主题尚未定义 `TextPrimaryColor` 等 `Color` 资源。

- [ ] **Step 3: 在资源字典开头创建完整调色板和语义画刷**

在 `AMacQGuiEditor.ps1` 的 `<Window.Resources>` 开始标签后、任何画刷前，添加以下颜色资源。保持当前深海极光实际色值，后续主题调整只改这里：

```xml
<Color x:Key="SurfaceAppStartColor">#263B68</Color>
<Color x:Key="SurfaceAppEndColor">#090E20</Color>
<Color x:Key="AuroraGlowStartColor">#3022D3EE</Color>
<Color x:Key="AuroraGlowMiddleColor">#1022D3EE</Color>
<Color x:Key="AuroraGlowEndColor">#0022D3EE</Color>
<Color x:Key="AccentCyanColor">#22D3EE</Color>
<Color x:Key="AccentIndigoColor">#6366F1</Color>
<Color x:Key="SurfaceSidebarStartColor">#1A243F</Color>
<Color x:Key="SurfaceSidebarEndColor">#10192D</Color>
<Color x:Key="SurfaceContentStartColor">#1B3F62</Color>
<Color x:Key="SurfaceContentEndColor">#0B1428</Color>
<Color x:Key="SurfacePanelStartColor">#1A3556</Color>
<Color x:Key="SurfacePanelEndColor">#0F2038</Color>
<Color x:Key="SurfaceInputStartColor">#132440</Color>
<Color x:Key="SurfaceInputEndColor">#0D1B32</Color>
<Color x:Key="SurfacePopupStartColor">#142942</Color>
<Color x:Key="SurfacePopupEndColor">#091526</Color>
<Color x:Key="TextPrimaryColor">#F7F2FF</Color>
<Color x:Key="TextBodyColor">#EDE7FF</Color>
<Color x:Key="TextSecondaryColor">#B9CAE0</Color>
<Color x:Key="TextListColor">#DCEBFA</Color>
<Color x:Key="AccentForegroundColor">#FFFFFFFF</Color>
<Color x:Key="BorderControlColor">#4E8FAE</Color>
<Color x:Key="BorderFocusColor">#5DD7FF</Color>
<Color x:Key="BorderDividerColor">#31506E</Color>
<Color x:Key="BorderPanelColor">#6488C4</Color>
<Color x:Key="ControlHoverColor">#1E5274</Color>
<Color x:Key="ControlPressedColor">#17415F</Color>
<Color x:Key="ScrollTrackColor">#10243A</Color>
<Color x:Key="ScrollThumbColor">#5577C8</Color>
<Color x:Key="ScrollThumbHoverColor">#71E1FF</Color>
<Color x:Key="DangerCloseHoverColor">#C42B4B</Color>
<Color x:Key="TitleBarDividerColor">#30FFFFFF</Color>
<Color x:Key="AnimationAppStartColor">#315C8F</Color>
<Color x:Key="AnimationAppEndColor">#0C142B</Color>
<Color x:Key="AnimationPanelStartColor">#204A70</Color>
<Color x:Key="AnimationPanelEndColor">#122944</Color>
<Color x:Key="AnimationInputStartColor">#183153</Color>
<Color x:Key="AnimationInputEndColor">#10213B</Color>
<Color x:Key="AnimationPopupStartColor">#1A3554</Color>
<Color x:Key="AnimationPopupEndColor">#0B1B30</Color>
```

紧随调色板，定义以下纯色语义画刷；保留已有渐变资源名，但将它们的 `GradientStop Color` 改为相应 `{StaticResource ...Color}`：

```xml
<SolidColorBrush x:Key="PrimaryTextBrush" Color="{StaticResource TextPrimaryColor}"/>
<SolidColorBrush x:Key="BodyTextBrush" Color="{StaticResource TextBodyColor}"/>
<SolidColorBrush x:Key="SecondaryTextBrush" Color="{StaticResource TextSecondaryColor}"/>
<SolidColorBrush x:Key="ListItemTextBrush" Color="{StaticResource TextListColor}"/>
<SolidColorBrush x:Key="AccentForegroundBrush" Color="{StaticResource AccentForegroundColor}"/>
<SolidColorBrush x:Key="ControlBorderBrush" Color="{StaticResource BorderControlColor}"/>
<SolidColorBrush x:Key="FocusBorderBrush" Color="{StaticResource BorderFocusColor}"/>
<SolidColorBrush x:Key="DividerBrush" Color="{StaticResource BorderDividerColor}"/>
<SolidColorBrush x:Key="PanelOutlineBrush" Color="{StaticResource BorderPanelColor}"/>
<SolidColorBrush x:Key="ControlHoverBrush" Color="{StaticResource ControlHoverColor}"/>
<SolidColorBrush x:Key="ControlPressedBrush" Color="{StaticResource ControlPressedColor}"/>
<SolidColorBrush x:Key="ScrollTrackBrush" Color="{StaticResource ScrollTrackColor}"/>
<SolidColorBrush x:Key="ScrollThumbBrush" Color="{StaticResource ScrollThumbColor}"/>
<SolidColorBrush x:Key="ScrollThumbHoverBrush" Color="{StaticResource ScrollThumbHoverColor}"/>
<SolidColorBrush x:Key="WindowCloseHoverBrush" Color="{StaticResource DangerCloseHoverColor}"/>
<SolidColorBrush x:Key="TitleBarDividerBrush" Color="{StaticResource TitleBarDividerColor}"/>
```

For each existing gradient, replace raw stops with the relevant `Color` resource. For example:

```xml
<LinearGradientBrush x:Key="AppBackgroundBrush" StartPoint="0,0" EndPoint="1,1">
  <GradientStop Color="{StaticResource SurfaceAppStartColor}" Offset="0"/>
  <GradientStop Color="{StaticResource SurfaceAppEndColor}" Offset="1"/>
</LinearGradientBrush>
```

Use the same pattern for `AuroraGlowBrush`, `AccentGradientBrush`, `SidebarSurfaceBrush`, `ContentSurfaceBrush`, `PanelSurfaceBrush`, `InputSurfaceBrush`, and `PopupSurfaceBrush`.

- [ ] **Step 4: 从资源字典读取动画终点色**

在 `Start-AnimatedBackground` 中，在 `$duration` 后添加下列本地变量：

```powershell
$animationAppStartColor = [Windows.Media.Color]$Window.Resources['AnimationAppStartColor']
$animationAppEndColor = [Windows.Media.Color]$Window.Resources['AnimationAppEndColor']
$animationPanelStartColor = [Windows.Media.Color]$Window.Resources['AnimationPanelStartColor']
$animationPanelEndColor = [Windows.Media.Color]$Window.Resources['AnimationPanelEndColor']
$animationInputStartColor = [Windows.Media.Color]$Window.Resources['AnimationInputStartColor']
$animationInputEndColor = [Windows.Media.Color]$Window.Resources['AnimationInputEndColor']
$animationPopupStartColor = [Windows.Media.Color]$Window.Resources['AnimationPopupStartColor']
$animationPopupEndColor = [Windows.Media.Color]$Window.Resources['AnimationPopupEndColor']
```

替换现有 `$animations` 与 `$surface` 数据，使 `Color` 直接保存这些变量：

```powershell
$animations = @(
    @{ Stop = $appBackgroundBrush.GradientStops[0]; Color = $animationAppStartColor }
    @{ Stop = $appBackgroundBrush.GradientStops[1]; Color = $animationAppEndColor }
)
foreach ($surface in @(
    @{ Key = 'PanelSurfaceBrush'; Colors = @($animationPanelStartColor, $animationPanelEndColor) }
    @{ Key = 'InputSurfaceBrush'; Colors = @($animationInputStartColor, $animationInputEndColor) }
    @{ Key = 'PopupSurfaceBrush'; Colors = @($animationPopupStartColor, $animationPopupEndColor) }
)) {
```

在动画循环中，将：

```powershell
$animation.To = [Windows.Media.ColorConverter]::new().ConvertFromString($item.Color)
```

替换为：

```powershell
$animation.To = [Windows.Media.Color]$item.Color
```

- [ ] **Step 5: 运行专用测试并确认调色板契约通过**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\TitleBarLayout.Tests.ps1
```

预期：退出码 `0`；静态断言和 XAML 解析均通过。

- [ ] **Step 6: 提交任务 1**

```bash
git add AMacQGuiEditor.ps1 tests/TitleBarLayout.Tests.ps1
git commit -m "refactor: centralize theme color palette"
```

### Task 2: 用语义画刷替换模板和运行时字段卡片的颜色字面量

**Files:**
- Modify: `tests/TitleBarLayout.Tests.ps1:5-100`
- Modify: `AMacQGuiEditor.ps1:249-332, 451-864`

**Interfaces:**
- Consumes: Task 1 的 `PrimaryTextBrush`、`BodyTextBrush`、`SecondaryTextBrush`、`ListItemTextBrush`、`AccentForegroundBrush`、`ControlBorderBrush`、`FocusBorderBrush`、`DividerBrush`、`PanelOutlineBrush`、`ScrollTrackBrush`、`ScrollThumbBrush`、`ScrollThumbHoverBrush`、`WindowCloseHoverBrush` 和现有渐变画刷。
- Produces: 控件模板与 `Build-FieldCards` 中没有可见颜色字面量，且所有状态均由语义资源表示。

- [ ] **Step 1: 写入会失败的字面量移除与运行时代码断言**

在 `tests/TitleBarLayout.Tests.ps1` 中添加：

```powershell
$productionUi = $content.Substring($content.IndexOf('<Window xmlns='), $content.IndexOf("'@", $content.IndexOf('<Window xmlns=')) - $content.IndexOf('<Window xmlns='))
$resourcesEnd = $productionUi.IndexOf('</Window.Resources>') + '</Window.Resources>'.Length
$templateAndLayout = $productionUi.Substring($resourcesEnd)
if ($templateAndLayout -match '(?i)(?<![A-Za-z0-9])#[0-9A-F]{3,8}(?![A-Za-z0-9])' -or
    $templateAndLayout -match 'Value="White"' -or
    $templateAndLayout -match 'Foreground="White"') {
    throw 'Control templates and layout must reference semantic brushes instead of color literals.'
}

$buildFieldCardsStart = $content.IndexOf('function Build-FieldCards')
$buildFieldCardsEnd = $content.IndexOf('function Fill-WeaponFields', $buildFieldCardsStart)
$buildFieldCards = $content.Substring($buildFieldCardsStart, $buildFieldCardsEnd - $buildFieldCardsStart)
if ($buildFieldCards -match 'BrushConverter|ConvertFromString|#[0-9A-Fa-f]{3,8}') {
    throw 'Build-FieldCards must resolve theme brushes from resources without hard-coded colors.'
}

foreach ($requiredRuntimeBrush in @(
    "FindResource('PrimaryTextBrush')",
    "FindResource('BodyTextBrush')",
    "FindResource('FocusBorderBrush')",
    "FindResource('DividerBrush')"
)) {
    if (!$buildFieldCards.Contains($requiredRuntimeBrush)) {
        throw "Build-FieldCards must resolve $requiredRuntimeBrush."
    }
}
```

- [ ] **Step 2: 运行测试并确认它因旧字面量失败**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\TitleBarLayout.Tests.ps1
```

预期：退出码非零，并包含 `Control templates and layout must reference semantic brushes` 或 `Build-FieldCards must resolve theme brushes`。

- [ ] **Step 3: 让 Build-FieldCards 只使用 FindResource 画刷**

在 `Build-FieldCards` 中删除：

```powershell
$bc = [Windows.Media.BrushConverter]::new()
```

在函数开始处使用以下完整资源获取块：

```powershell
$primaryTextBrush = $FieldCardsGrid.FindResource('PrimaryTextBrush')
$bodyTextBrush = $FieldCardsGrid.FindResource('BodyTextBrush')
$secondaryTextBrush = $FieldCardsGrid.FindResource('SecondaryTextBrush')
$panelSurfaceBrush = $FieldCardsGrid.FindResource('PanelSurfaceBrush')
$inputSurfaceBrush = $FieldCardsGrid.FindResource('InputSurfaceBrush')
$controlBorderBrush = $FieldCardsGrid.FindResource('ControlBorderBrush')
$dividerBrush = $FieldCardsGrid.FindResource('DividerBrush')
$focusBorderBrush = $FieldCardsGrid.FindResource('FocusBorderBrush')
```

使用这些变量替换字段函数中剩余颜色转换：

```powershell
$label.Foreground = $bodyTextBrush
$ctrl.CaretBrush = $focusBorderBrush
$ctrl.Foreground = $primaryTextBrush
$line.Height = 1; $line.Background = $dividerBrush
```

保留此前已经资源化的 `$header.Foreground`、`$outer.Background`、`$outer.BorderBrush`、`$ctrl.Background` 和 `$ctrl.BorderBrush`。

- [ ] **Step 4: 用语义画刷替换所有 XAML 控件颜色**

在 `AMacQGuiEditor.ps1` 的 `</Window.Resources>` 后到 XAML 结束之间，按以下映射逐项替换：

| 原用途 | 替换值 |
|---|---|
| 主标题、默认控件、侧栏按钮文字 | `{DynamicResource PrimaryTextBrush}` |
| 字段标签、标题栏按钮文字 | `{DynamicResource BodyTextBrush}` |
| 说明和分组标题、下拉箭头 | `{DynamicResource SecondaryTextBrush}` |
| 下拉选项与枪械列表文字 | `{DynamicResource ListItemTextBrush}` |
| 焦点边框和输入插入符 | `{DynamicResource FocusBorderBrush}` |
| 分区、字段分隔线和侧栏边界 | `{DynamicResource DividerBrush}` |
| 枪械列表外框 | `{DynamicResource PanelOutlineBrush}` |
| 滚动条轨道、滑块、滑块悬停/拖动 | `{DynamicResource ScrollTrackBrush}`、`{DynamicResource ScrollThumbBrush}`、`{DynamicResource ScrollThumbHoverBrush}` |
| 青靛强调渐变上的文字 | `{DynamicResource AccentForegroundBrush}` |
| 关闭按钮悬停 Background | `{DynamicResource WindowCloseHoverBrush}` |
| 标题栏分隔线 BorderBrush | `{DynamicResource TitleBarDividerBrush}`，并删除局部 `Opacity="0.18"` |

将列表/下拉的已选前景、保存按钮前景和非活动选中项前景改为 `{DynamicResource AccentForegroundBrush}`。保留 `Background="Transparent"` 的结构性层；不得再出现 `#...`、`White`、`#FFFFFFFF` 或其等价的可见颜色字符串于资源区外。

- [ ] **Step 5: 运行专用测试与 XAML 解析验证**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\TitleBarLayout.Tests.ps1
```

预期：退出码 `0`；`[Windows.Markup.XamlReader]::Parse($xaml)` 不抛异常。

- [ ] **Step 6: 提交任务 2**

```bash
git add AMacQGuiEditor.ps1 tests/TitleBarLayout.Tests.ps1
git commit -m "refactor: use semantic theme brushes"
```

### Task 3: 回归、构建新版 EXE 并启动验证

**Files:**
- Modify: `AMacQGuiEditor.ps1`（仅在测试发现与集中资源契约不一致时修正）
- Modify: `tests/TitleBarLayout.Tests.ps1`（仅在断言误判有效资源引用时修正；不得降低调色板或字面量移除约束）
- Generated: `dist/AMacQ配置编辑器.exe`（Git 已忽略；由构建验证生成）

**Interfaces:**
- Consumes: Task 1 和 Task 2 的集中资源主题、现有 `Build-Release.ps1`。
- Produces: 已由新源码构建的 `dist/AMacQ配置编辑器.exe`，以及测试和启动验证证据。

- [ ] **Step 1: 运行完整 PowerShell 回归套件**

运行：

```powershell
Get-ChildItem .\tests\*.Tests.ps1 | ForEach-Object {
    Write-Host "Running $($_.Name)"
    & powershell -NoProfile -ExecutionPolicy Bypass -File $_.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

预期：`BuildRelease.Tests.ps1`、`IconResource.Tests.ps1` 与 `TitleBarLayout.Tests.ps1` 全部以退出码 `0` 结束。`IconResource.Tests.ps1` 会重建 ICO；检查后不得把 `assets/AMacQ.ico` 的无意二进制变化作为主题提交内容。

- [ ] **Step 2: 构建新版 EXE**

运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

预期：命令完成且存在 `dist\AMacQ配置编辑器.exe`。该文件是被忽略的构建产物；不要 `git add` 它。

- [ ] **Step 3: 验证新版 EXE 可启动**

运行：

```powershell
$exe = Join-Path $PWD 'dist\AMacQ配置编辑器.exe'
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 3
if ($process.HasExited) {
    throw "The packaged application exited early with code $($process.ExitCode)."
}
Stop-Process -Id $process.Id -Force
Write-Output 'The packaged application remained running for 3 seconds without a startup error.'
```

预期：输出 `The packaged application remained running for 3 seconds without a startup error.`；没有 XAML 解析或资源查找错误。

- [ ] **Step 4: 手工视觉检查新版 EXE**

双击 `dist\AMacQ配置编辑器.exe`，检查后正常关闭：

1. 标题栏与主体保持连续深海极光背景，无扫描线。
2. 下拉框关闭、悬停、焦点和弹出状态均使用深海蓝/蓝青资源主题。
3. 选择任意枪械后，运行时生成的字段卡片、文本框与下拉框文字和边框均可读。
4. 保存按钮和列表选中项仍使用青色至靛蓝渐变及高对比文字。
5. 关闭按钮悬停时仍为红色，其他标题栏/侧栏按钮保持蓝青交互色。

- [ ] **Step 5: 检查变更范围与工作区**

运行：

```bash
git diff --check
git status --short
git diff -- AMacQGuiEditor.ps1 tests/TitleBarLayout.Tests.ps1 docs/superpowers/specs/2026-07-28-deep-ocean-aurora-theme-design.md
```

预期：`git diff --check` 无输出；跟踪文件变更仅限源码、主题测试和已批准规格/计划，`dist/` 不被跟踪。

- [ ] **Step 6: 提交任务 3**

```bash
git add AMacQGuiEditor.ps1 tests/TitleBarLayout.Tests.ps1 docs/superpowers/specs/2026-07-28-deep-ocean-aurora-theme-design.md docs/superpowers/plans/2026-07-28-deep-ocean-aurora-theme-implementation.md docs/superpowers/plans/2026-07-28-theme-color-tokens-implementation.md
git commit -m "build: package centralized deep ocean theme"
```
