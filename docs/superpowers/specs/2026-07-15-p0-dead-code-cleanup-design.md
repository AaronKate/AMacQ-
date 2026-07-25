# P0 废弃与重复代码清理设计

## 目标

在不改变界面、Lua 配置读写、启动行为或保存逻辑的前提下，删除审查已证实无引用、重复或无视觉作用的代码。

## 清理范围

仅修改 `AMacQGuiEditor.ps1`：

1. 删除未使用的 `$script:WeaponVarSuffix` 常量。
2. 删除重复的 `$window.Height = 600`；窗口 XAML 已声明 `Height="600"`。
3. 删除与 XAML 重复的运行时主题赋值：
   - `$titleLabel.Foreground`
   - `$weaponList.Background`
   - `$weaponList.Foreground`
   - `$weaponList.BorderBrush`
   - `$saveBtn.Background`
4. 删除因此不再需要的 `$bc = [Windows.Media.BrushConverter]::new()`。
5. 保留 `$selectedLbl.Foreground`，因为 XAML 默认颜色不同，运行时赋值仍用于显示当前选中枪械的蓝色标题。

## 不在本次范围

- 不修改 `WeaponListItem` 自定义模板及其失焦选中态逻辑。
- 不修改 `press`、`modeswitch` 全局设置和 Lua 字符串读写辅助函数。
- 不合并数值与字符串 Lua 读写函数。
- 不修改原子写入、编码保留、按键冲突清理、字段顺序或旧按键兼容机制。
- 不拆分 `Start-Gui`、不修改 VBS 启动器。

## 验证

- 脚本 PowerShell 解析通过。
- 被删除的符号和运行时赋值不再存在。
- 可启动窗口，并确认枪械标题保持蓝色、武器列表仍使用自定义选中样式、保存按钮仍为蓝色。
