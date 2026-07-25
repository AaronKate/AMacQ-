# 融合式标题栏 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使自定义标题栏按左右分区连续延伸到下方侧栏与内容区背景，并只保留关闭按钮。

**Architecture:** 保留现有 `WindowChrome` 和 38px 标题栏。将标题栏改为两列：固定 220px 左列复用 `PurpleSidebarBrush`，右列复用 `PurpleContentBrush`；删除标题栏底部边线。移除最小化、最大化/还原控件和关联 PowerShell 事件，仅保留关闭按钮。

**Tech Stack:** Windows PowerShell、WPF、XAML、`System.Windows.Shell.WindowChrome`。

## Global Constraints

- 仅修改 `AMacQGuiEditor.ps1`。
- 保留窗口拖动、双击标题栏最大化/还原、边缘缩放和最大化工作区处理。
- 不修改配置读取、保存、动画、侧栏或内容区内部功能。
- 标题栏左列必须使用 `PurpleSidebarBrush`，右列必须使用 `PurpleContentBrush`。
- 标题栏不保留底部边线；只保留关闭按钮及其红色悬停效果。

---

### Task 1: 使标题栏背景与主布局连续并移除最小化/最大化控件

**Files:**
- Modify: `AMacQGuiEditor.ps1:656-679`
- Modify: `AMacQGuiEditor.ps1:820-845`
- Modify: `AMacQGuiEditor.ps1:1024-1045`

**Interfaces:**
- Consumes: 现有 XAML 资源 `PurpleSidebarBrush`、`PurpleContentBrush`、`CloseTitleBarButton`，以及现有 `CloseBtn`。
- Produces: 只有 `CloseBtn` 的连续分区标题栏；`WindowChrome CaptionHeight="38"` 保持不变。

- [ ] **Step 1: 写入失败的静态检查**

运行：

```powershell
$content = Get-Content -Raw .\AMacQGuiEditor.ps1
if ($content -match 'Grid.ColumnDefinitions' -and
    $content -match 'Background="{StaticResource PurpleSidebarBrush}"' -and
    $content -match 'Background="{StaticResource PurpleContentBrush}"' -and
    $content -notmatch 'Name="MinimizeBtn"' -and
    $content -notmatch 'Name="MaximizeBtn"') {
    exit 0
}
throw 'Seamless title-bar layout is required.'
```

预期：命令以错误 `Seamless title-bar layout is required.` 退出，因为当前标题栏为独立渐变，且仍有最小化和最大化按钮。

- [ ] **Step 2: 替换标题栏 XAML**

将当前 `TitleBar`：

```xml
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
```

替换为：

```xml
<Grid Name="TitleBar" Grid.Row="0">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="220"/>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>

  <Border Background="{StaticResource PurpleSidebarBrush}">
    <TextBlock Text="AMacQ Configuration Editor" Margin="14,0,0,0"
               VerticalAlignment="Center" FontSize="13" Foreground="#F7F2FF"/>
  </Border>

  <Border Grid.Column="1" Background="{StaticResource PurpleContentBrush}">
    <StackPanel HorizontalAlignment="Right" Orientation="Horizontal"
                shell:WindowChrome.IsHitTestVisibleInChrome="True">
      <Button Name="CloseBtn" Content="&#xE8BB;" Style="{StaticResource CloseTitleBarButton}"/>
    </StackPanel>
  </Border>
</Grid>
```

不要添加 `BorderBrush` 或 `BorderThickness`；标题栏与下方区域之间不得有分隔线。

- [ ] **Step 3: 移除最小化/最大化控件查找与状态同步脚本**

删除以下代码：

```powershell
$minimizeBtn = $window.FindName('MinimizeBtn')
$maximizeBtn = $window.FindName('MaximizeBtn')
$updateMaximizeButton = {
    $maximizeBtn.Content = if ($window.WindowState -eq [Windows.WindowState]::Maximized) { [char]0xE923 } else { [char]0xE922 }
}
```

保留：

```powershell
$closeBtn = $window.FindName('CloseBtn')
```

- [ ] **Step 4: 移除最小化/最大化事件绑定**

从 “Wire events” 区域删除：

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
$window.Add_StateChanged($updateMaximizeButton)
& $updateMaximizeButton
```

保留关闭事件：

```powershell
$closeBtn.Add_Click({
    $window.Close()
})
```

- [ ] **Step 5: 验证连续布局、语法和变更格式**

运行：

```bash
powershell.exe -NoProfile -Command '$content = Get-Content -Raw ".\AMacQGuiEditor.ps1"; if ($content -notmatch "Name=`"TitleBar`"" -or $content -notmatch "Background=`"{StaticResource PurpleSidebarBrush}`"" -or $content -notmatch "Background=`"{StaticResource PurpleContentBrush}`"" -or $content -match "Name=`"MinimizeBtn`"|Name=`"MaximizeBtn`"") { throw "Seamless title-bar layout is invalid." }; if ($content -match "\$minimizeBtn|\$maximizeBtn|Add_StateChanged") { throw "Removed title-bar controls are still referenced." }'
powershell.exe -NoProfile -Command '$tokens = $null; $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"), [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { $_.ToString() }; exit 1 }'
git diff --check
```

预期：全部退出码为 `0`。

- [ ] **Step 6: 运行窗口冒烟测试和手动验证**

运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\AMacQGuiEditor.ps1
```

检查：标题栏左侧背景连续进入侧栏、右侧背景连续进入内容区，二者间无标题栏底线；只显示关闭按钮，且其点击与红色悬停效果正常；标题栏拖动、双击最大化/还原、边缘缩放仍可用；选择文件和“应用”仍能正常操作。关闭窗口后命令退出。

- [ ] **Step 7: 审核并提交**

运行：

```bash
git diff -- AMacQGuiEditor.ps1
git status --short
git add AMacQGuiEditor.ps1
git commit -m "Blend title bar into application theme"
```

预期：功能提交仅包含 `AMacQGuiEditor.ps1`。

## Self-Review

- **Spec coverage:** 标题栏的两列分别复用现有侧栏和内容画刷，取消底线；删除最小化和最大化控件、事件与状态同步，保留关闭按钮和 WindowChrome 行为。
- **Placeholder scan:** 所有替换代码、删除代码和验证命令完整明确。
- **Type consistency:** 保留的 `CloseBtn` 名称与 PowerShell `FindName` 及事件绑定一致；移除的变量不存在后续引用。
