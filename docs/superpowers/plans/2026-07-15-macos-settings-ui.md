# macOS 系统设置风格界面实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 AMacQ 配置编辑器调整为 macOS System Settings 式的原生设置页布局，同时保持全部配置读写行为不变。

**Architecture:** 只重写 `Start-Gui` 内嵌 XAML 的布局和 `Build-FieldCards` 的显示树。仍使用相同控件名称（`WeaponList`、`MouseModelList`、`FieldCards`、`SaveBtn` 等），使现有事件处理、鼠标预设与 Lua 保存逻辑无须改变。

**Tech Stack:** Windows PowerShell 5.1、WPF / PresentationFramework、内嵌 XAML。

## Global Constraints

- 保持项目为零依赖的 PowerShell + WPF 桌面工具。
- `AMacQGuiEditor.ps1` 必须以 UTF-8 with BOM 保存，确保 Windows PowerShell 5.1 正确读取中文。
- 不修改 Lua 变量、文件读写、编码保留、鼠标型号逻辑、按键冲突重置逻辑或保存逻辑。
- 保留现有命名控件：`FolderPath`、`RefreshBtn`、`BrowseBtn`、`MouseModelList`、`WeaponList`、`SelectedLabel`、`FieldCards`、`SaveBtn`。

---

### Task 1: 将窗口改为系统设置式的侧栏和详情页布局

**Files:**
- Modify: `AMacQGuiEditor.ps1:330-445`

**Interfaces:**
- Consumes：现有命名控件和事件绑定。
- Produces：同名 WPF 控件；不改变任何控件类型。

- [ ] **Step 1: 调整 XAML 根布局**

将主 Grid 改成两列：左侧固定 `220` 宽导航栏，右侧为详情页；移除现有“左白色卡片 + 右滚动区”的大卡片布局。

```xml
<Grid Background="#F5F5F7">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="220"/>
    <ColumnDefinition Width="*"/>
  </Grid.ColumnDefinitions>

  <Border Grid.Column="0" Background="#ECECF0" BorderBrush="#D1D1D6"
          BorderThickness="0,0,1,0">
    <!-- Sidebar -->
  </Border>

  <Grid Grid.Column="1" Margin="32,24,32,24">
    <!-- Detail page -->
  </Grid>
</Grid>
```

- [ ] **Step 2: 构建左侧导航栏**

将现有 `WeaponList` 放入侧栏，使用分组标题“武器”，并保留 `WeaponList` 名称：

```xml
<StackPanel Margin="16,22,16,16">
  <TextBlock Text="AMacQ" FontSize="18" FontWeight="SemiBold"
             Foreground="#1D1D1F" Margin="8,0,0,28"/>
  <TextBlock Text="武器" FontSize="12" FontWeight="SemiBold"
             Foreground="#6E6E73" Margin="8,0,0,8"/>
  <ListBox Name="WeaponList" BorderThickness="0" Background="Transparent"
           Foreground="#1D1D1F" FontSize="13"/>
</StackPanel>
```

- [ ] **Step 3: 构建右侧详情页框架**

右侧详情页从上到下包含：标题、路径/操作、鼠标型号组、按键组、灵敏度组、右下保存按钮。保留 `FolderPath`、`RefreshBtn`、`BrowseBtn`、`MouseModelList`、`SelectedLabel`、`FieldCards`、`SaveBtn`。

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="Auto"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="Auto"/>
  </Grid.RowDefinitions>
  <!-- header, path/actions, scrollable settings, footer -->
</Grid>
```

- [ ] **Step 4: 验证 XAML 和控件名称**

运行：

```powershell
powershell.exe -NoProfile -Command '$t=$null;$e=$null;[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"),[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|%{$_.ToString()};exit 1};"PASSED"'
```

预期：输出 `PASSED`。

- [ ] **Step 5: Commit**

当前工作目录不是 Git 仓库，不执行提交。

### Task 2: 将字段显示改为 macOS 连续设置列表

**Files:**
- Modify: `AMacQGuiEditor.ps1:220-290`

**Interfaces:**
- Consumes：`$script:FieldDefs` 和 `Build-FieldCards($FieldCardsGrid)`。
- Produces：同样返回 `$inputBoxes` 哈希表，键名格式仍为 `"$file|$VarSuffix"`。

- [ ] **Step 1: 按文件建立两个设置组**

每个文件组创建一个包含标题和白色列表容器的 StackPanel：

```powershell
$groupTitle = New-Object Windows.Controls.TextBlock
$groupTitle.Text = if ($file -eq 'sorinkg.lua') { '按键' } else { '灵敏度' }
$groupTitle.FontSize = 12
$groupTitle.FontWeight = 'SemiBold'
$groupTitle.Foreground = $bc.ConvertFromString('#6E6E73')
$groupTitle.Margin = '0,0,0,8'
```

- [ ] **Step 2: 用连续行取代字段卡片**

每个字段创建高度 `44` 的 Grid 行，左侧标签，右侧控件；在非最后一项后加 `#E5E5EA` 底部分割线。每个列表容器使用白色背景、`CornerRadius=10`。

```powershell
$row = New-Object Windows.Controls.Grid
$row.Height = 44
$row.ColumnDefinitions.Add((New-Object Windows.Controls.ColumnDefinition -Property @{ Width='*' }))
$row.ColumnDefinitions.Add((New-Object Windows.Controls.ColumnDefinition -Property @{ Width='150' }))

$label = New-Object Windows.Controls.TextBlock
$label.Text = $field.Label
$label.FontSize = 13
$label.Foreground = $bc.ConvertFromString('#1D1D1F')
$label.VerticalAlignment = 'Center'
$label.Margin = '14,0,8,0'
[Windows.Controls.Grid]::SetColumn($label, 0)
```

- [ ] **Step 3: 调整输入控件外观**

ComboBox 和 TextBox 使用紧凑原生设置控件外观：高度 `30`、背景 `#F5F5F7`、边框 `#C7C7CC`、边框 1px、圆角由外包 `Border CornerRadius=6` 实现，右侧宽度 `140`。

- [ ] **Step 4: 将 FieldCards 改为纵向设置组容器**

`FieldCards` 不再使用两列 `UniformGrid`；在 XAML 改为 `StackPanel Name="FieldCards"`，函数中只设置 Children，不设置 `Rows` 或 `Columns`。

- [ ] **Step 5: 验证字段键不变**

在 PowerShell 控制台 dot-source 后检查：

```powershell
. .\AMacQGuiEditor.ps1
if ($script:FieldDefs.Count -ne 7) { throw '字段定义数量错误' }
if (($script:FieldDefs | Where-Object File -eq 'sorinkg.lua').Count -ne 3) { throw '按键字段数量错误' }
if (($script:FieldDefs | Where-Object File -eq 'sorinxs.lua').Count -ne 4) { throw '灵敏度字段数量错误' }
'PASSED'
```

预期：输出 `PASSED`。

- [ ] **Step 6: Commit**

当前工作目录不是 Git 仓库，不执行提交。

### Task 3: 应用系统设置颜色、选中状态和保存按钮样式

**Files:**
- Modify: `AMacQGuiEditor.ps1:470-510`

**Interfaces:**
- Consumes：`WeaponList`、`SaveBtn` 和现有 BrushConverter。
- Produces：与 `WeaponList.ItemContainerStyle` 兼容的侧栏选中样式。

- [ ] **Step 1: 调整窗口和文本颜色**

使用以下颜色：

```powershell
$window.Background = $bc.ConvertFromString('#F5F5F7')
$titleLabel.Foreground = $bc.ConvertFromString('#1D1D1F')
$folderPath.Foreground = $bc.ConvertFromString('#6E6E73')
$selectedLbl.Foreground = $bc.ConvertFromString('#1D1D1F')
$saveBtn.Background = $bc.ConvertFromString('#007AFF')
```

- [ ] **Step 2: 配置侧栏选中项**

保持透明默认背景；选中时使用 `#007AFF` 背景、白色字体，圆角 6px。通过 ListBoxItem ControlTemplate 的 Border 实现圆角，而不是只使用 Background setter。

- [ ] **Step 3: 调整保存按钮**

保存按钮为紧凑矩形，`Padding="18,8"`、`FontSize="14"`、圆角 `7`。保留现有 Content 更改逻辑（`保存` / `保存成功`）。

- [ ] **Step 4: 最终验证**

1. 语法：执行 Task 1 Step 4 命令，预期 `PASSED`。
2. 启动 `.bat`，选择一个 AMacQ 目录、鼠标型号和武器；确认按键和灵敏度都能显示。
3. 修改一项按键或灵敏度并保存；确认 Lua 写入及冲突重置行为未变化。
4. 保存后确认按钮先显示“保存成功”，随后恢复“保存”。

- [ ] **Step 5: 写回 UTF-8 BOM**

```powershell
powershell.exe -NoProfile -Command '$p=Resolve-Path ".\AMacQGuiEditor.ps1";$c=[System.IO.File]::ReadAllText($p,[System.Text.UTF8Encoding]::new($false));[System.IO.File]::WriteAllText($p,$c,[System.Text.UTF8Encoding]::new($true));"BOM restored"'
```

预期：输出 `BOM restored`。

- [ ] **Step 6: Commit**

当前工作目录不是 Git 仓库，不执行提交。
