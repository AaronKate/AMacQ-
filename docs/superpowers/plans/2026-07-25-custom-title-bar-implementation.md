# 自定义标题栏 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 以与 AMacQ 主界面一致的蓝紫渐变替换 Windows 原生标题栏，并保留拖动、缩放、最大化/还原、最小化和关闭行为。

**Architecture:** 删除当前 DWM 深色原生标题栏函数，令 WPF `Window` 使用 `WindowStyle="None"` 和 `WindowChrome`。在现有根布局上方增加 38px 的标题栏行；`WindowChrome` 提供拖动、边缘缩放和最大化工作区逻辑，PowerShell 事件处理器负责三个自绘控制按钮与图标状态。

**Tech Stack:** Windows PowerShell、WPF、`System.Windows.Shell.WindowChrome`、XAML。

## Global Constraints

- 仅修改 `AMacQGuiEditor.ps1`。
- 移除当前 `Set-DarkTitleBar` DWM P/Invoke 函数和 `SourceInitialized` 调用。
- 不修改配置读取、保存、动画、侧栏或内容区的既有功能。
- 标题栏背景使用 `#26345E` 至 `#182243` 的蓝紫渐变。
- 使用 Windows 风格的右上角最小化、最大化/还原和关闭按钮；关闭悬停色为红色。

---

### Task 1: 定义自定义标题栏窗口和视觉资源

**Files:**
- Modify: `AMacQGuiEditor.ps1:419-617`

**Interfaces:**
- Consumes: 现有 XAML 的 `PurpleSidebarBrush`、`AccentGradientBrush` 和窗口根 `Grid`。
- Produces: `TitleBar`、`MinimizeBtn`、`MaximizeBtn`、`CloseBtn` 三个命名控件，以及保留边缘缩放的 `WindowChrome`。

- [ ] **Step 1: 写入静态需求检查并确认当前脚本不满足**

运行：

```powershell
$content = Get-Content -Raw .\AMacQGuiEditor.ps1
if ($content -match 'Name="TitleBar"' -and $content -match 'Name="MinimizeBtn"' -and $content -match 'WindowStyle="None"') {
    exit 0
}
throw 'Custom title-bar XAML is required.'
```

预期：命令以错误 `Custom title-bar XAML is required.` 退出，证明当前脚本仍使用原生标题栏。

- [ ] **Step 2: 更新 Window 根元素和 WindowChrome 配置**

将 Window 起始标签：

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="AMacQ Configuration Editor" Height="600" Width="860"
        MinHeight="520" MinWidth="760" WindowStartupLocation="CenterScreen"
        Background="#20193D" Foreground="#F7F2FF" FontFamily="Segoe UI">
```

替换为：

```xml
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="AMacQ Configuration Editor" Height="600" Width="860"
        MinHeight="520" MinWidth="760" WindowStartupLocation="CenterScreen"
        WindowStyle="None" ResizeMode="CanResize"
        Background="#20193D" Foreground="#F7F2FF" FontFamily="Segoe UI">
  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight="38" ResizeBorderThickness="6" GlassFrameThickness="0" CornerRadius="0" UseAeroCaptionButtons="False"/>
  </shell:WindowChrome.WindowChrome>
```

- [ ] **Step 3: 添加标题栏资源样式**

在 `</Window.Resources>` 前加入：

```xml
<Style x:Key="TitleBarButton" TargetType="Button">
  <Setter Property="Width" Value="46"/>
  <Setter Property="Height" Value="38"/>
  <Setter Property="Foreground" Value="#EDE7FF"/>
  <Setter Property="Background" Value="Transparent"/>
  <Setter Property="BorderThickness" Value="0"/>
  <Setter Property="FontFamily" Value="Segoe MDL2 Assets"/>
  <Setter Property="FontSize" Value="10"/>
  <Setter Property="Template">
    <Setter.Value>
      <ControlTemplate TargetType="Button">
        <Border x:Name="bd" Background="{TemplateBinding Background}">
          <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property="IsMouseOver" Value="True">
            <Setter TargetName="bd" Property="Background" Value="#3856B8"/>
          </Trigger>
          <Trigger Property="IsPressed" Value="True">
            <Setter TargetName="bd" Property="Background" Value="#2A4FAD"/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>
<Style x:Key="CloseTitleBarButton" TargetType="Button" BasedOn="{StaticResource TitleBarButton}">
  <Style.Triggers>
    <Trigger Property="IsMouseOver" Value="True">
      <Setter Property="Background" Value="#C42B4B"/>
    </Trigger>
  </Style.Triggers>
</Style>
```

- [ ] **Step 4: 将根 Grid 改为标题栏与现有内容两行**

将 `<Grid>` 之后的现有两列定义替换为以下骨架；现有的两列、`SidebarPanel` 和 `ContentPanel` 内容放进 `Grid.Row="1"` 的嵌套 Grid，内部 XAML 保持不变：

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="38"/>
    <RowDefinition Height="*"/>
  </Grid.RowDefinitions>

  <Border Name="TitleBar" Grid.Row="0" BorderBrush="#4A3A70" BorderThickness="0,0,0,1">
    <Border.Background>
      <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
        <GradientStop Color="#26345E" Offset="0"/>
        <GradientStop Color="#182243" Offset="1"/>
      </LinearGradientBrush>
    </Border.Background>
    <Grid>
      <TextBlock Text="AMacQ Configuration Editor" Margin="14,0,0,0"
                 VerticalAlignment="Center" FontSize="13" Foreground="#F7F2FF"/>
      <StackPanel HorizontalAlignment="Right" Orientation="Horizontal"
                  shell:WindowChrome.IsHitTestVisibleInChrome="True">
        <Button Name="MinimizeBtn" Content="&#xE921;" Style="{StaticResource TitleBarButton}"/>
        <Button Name="MaximizeBtn" Content="&#xE922;" Style="{StaticResource TitleBarButton}"/>
        <Button Name="CloseBtn" Content="&#xE8BB;" Style="{StaticResource CloseTitleBarButton}"/>
      </StackPanel>
    </Grid>
  </Border>

  <Grid Grid.Row="1">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="220"/>
      <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
    <!-- 将原有 SidebarPanel 和 ContentPanel 完整置于此处。 -->
  </Grid>
</Grid>
```

`SidebarPanel` 和 `ContentPanel` 的原有 `Grid.Column` 值保留；不要在它们内部改变任何行、列、资源或控件。

- [ ] **Step 5: 验证 XAML 标记已出现且 PowerShell 可解析**

运行：

```bash
powershell.exe -NoProfile -Command '$content = Get-Content -Raw ".\AMacQGuiEditor.ps1"; if ($content -notmatch "Name=\"TitleBar\"" -or $content -notmatch "Name=\"MinimizeBtn\"" -or $content -notmatch "WindowStyle=\"None\"") { throw "Custom title-bar XAML is missing." }'
powershell.exe -NoProfile -Command '$tokens = $null; $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"), [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { $_.ToString() }; exit 1 }'
```

预期：两条命令均以退出码 `0` 完成。

### Task 2: 绑定控制按钮、删除 DWM 逻辑并验证窗口交互

**Files:**
- Modify: `AMacQGuiEditor.ps1:212-247`
- Modify: `AMacQGuiEditor.ps1:793-1000`

**Interfaces:**
- Consumes: Task 1 生成的 `MinimizeBtn`、`MaximizeBtn`、`CloseBtn` 和 WPF `$window`。
- Produces: 三个窗口控制按钮行为，以及随 `WindowState` 更改的最大化/还原图标。

- [ ] **Step 1: 写入事件绑定缺失检查并确认失败**

运行：

```powershell
$content = Get-Content -Raw .\AMacQGuiEditor.ps1
if ($content -match 'MinimizeBtn' -and $content -match 'Add_StateChanged' -and $content -match 'Add_Click') {
    exit 0
}
throw 'Custom title-bar event handlers are required.'
```

预期：命令以错误 `Custom title-bar event handlers are required.` 退出。

- [ ] **Step 2: 删除 DWM 代码和 SourceInitialized 调用**

删除整个 `Set-DarkTitleBar` 函数（从 `function Set-DarkTitleBar {` 到对应的 `}`），并删除：

```powershell
$window.Add_SourceInitialized({
    Set-DarkTitleBar $this
})
```

不保留 `dwmapi.dll`、`AMacQ.NativeMethods` 或 `DwmSetWindowAttribute` 引用。

- [ ] **Step 3: 查找标题栏控件并新增窗口状态辅助脚本块**

在现有控件查找区、`$refreshBtn = ...` 前加入：

```powershell
$minimizeBtn = $window.FindName('MinimizeBtn')
$maximizeBtn = $window.FindName('MaximizeBtn')
$closeBtn = $window.FindName('CloseBtn')

$updateMaximizeButton = {
    $maximizeBtn.Content = if ($window.WindowState -eq [Windows.WindowState]::Maximized) { [char]0xE923 } else { [char]0xE922 }
}
```

- [ ] **Step 4: 在既有 “Wire events” 区域绑定标题栏事件**

在现有按钮事件绑定前加入：

```powershell
$minimizeBtn.Add_Click({
    $window.WindowState = [Windows.WindowState]::Minimized
})
$maximizeBtn.Add_Click({
    $window.WindowState = if ($window.WindowState -eq [Windows.WindowState]::Maximized) {
        [Windows.WindowState]::Normal
    } else {
        [Windows.WindowState]::Maximized
    }
})
$closeBtn.Add_Click({
    $window.Close()
})
$window.Add_StateChanged($updateMaximizeButton)
& $updateMaximizeButton
```

标题栏拖动和双击行为由 Task 1 的 `WindowChrome CaptionHeight="38"` 负责；不要对 `TitleBar` 添加 `DragMove()` 鼠标事件，以避免与 `WindowChrome` 的非客户区命中测试冲突。

- [ ] **Step 5: 运行静态检查与脚本解析**

运行：

```bash
powershell.exe -NoProfile -Command '$content = Get-Content -Raw ".\AMacQGuiEditor.ps1"; if ($content -match "Set-DarkTitleBar|DwmSetWindowAttribute|Add_SourceInitialized") { throw "DWM title-bar code was not removed." }; if ($content -notmatch "\$minimizeBtn\.Add_Click" -or $content -notmatch "\$maximizeBtn\.Add_Click" -or $content -notmatch "\$closeBtn\.Add_Click" -or $content -notmatch "Add_StateChanged") { throw "Custom title-bar event handlers are missing." }'
powershell.exe -NoProfile -Command '$tokens = $null; $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"), [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { $_.ToString() }; exit 1 }'
git diff --check
```

预期：所有命令退出码为 `0`。

- [ ] **Step 6: 手动窗口验证**

运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\AMacQGuiEditor.ps1
```

检查：标题栏为 `#26345E → #182243` 渐变；空白标题栏区域可拖动与双击最大化/还原；右上角三个按钮有效；最大化按钮状态会切换；窗口边缘可缩放；最大化不覆盖任务栏；选择文件、编辑与应用流程仍可运行。关闭窗口后命令退出。

- [ ] **Step 7: 审核并提交功能变更**

运行：

```bash
git diff -- AMacQGuiEditor.ps1
git status --short
git add AMacQGuiEditor.ps1
git commit -m "Replace native title bar with custom chrome"
```

预期：此功能提交仅包含 `AMacQGuiEditor.ps1`。

## Self-Review

- **Spec coverage:** Task 1 实现无原生标题栏、38px 渐变标题栏、Windows 风格控制按钮和 WindowChrome；Task 2 删除 DWM 逻辑、绑定按钮并同步状态。验证步骤覆盖解析、窗口交互、任务栏边界和既有功能。
- **Placeholder scan:** 所有代码、命令和所需控件名称均已明确；嵌套 Grid 中的现有内容要求是机械移动而非新增未定义行为。
- **Type consistency:** XAML 控件名与 PowerShell `FindName` 和事件变量一致；最大化状态使用 `Windows.WindowState`，图标字体和码位集中在 XAML/状态辅助脚本块中。
