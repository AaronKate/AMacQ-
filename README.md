# AMacQ 配置编辑器

用于编辑 AMacQ Lua 配置文件的 Windows 桌面工具。

本工具只会读取和修改你在界面中主动选择的本地配置文件；不会上传配置文件，也不会与游戏进程交互。

## 功能

- 分别选择按键配置与灵敏度配置两个 Lua 文件
- 自动读取并列出配置中的枪械
- 按鼠标型号编辑枪械的无修饰键、Alt 修饰键和 Ctrl 修饰键
- 编辑 X/Y 灵敏度及灵敏度增幅 X/Y 数值
- 设置触发方式和灵敏度增幅激活键
- 保存时保留原文件编码，并先写入临时文件再替换原文件

## 运行环境

- Windows
- 已安装 Windows PowerShell（系统自带）
- 系统具备 WPF 图形界面组件

## 启动

发布版本中，双击根目录的：

```text
AMacQ配置编辑器.exe
```

即可启动图形编辑器，无需保留或运行 PowerShell 启动器。

### 开发者构建

主程序源码为 `AMacQGuiEditor.ps1`。修改源码后，在项目根目录的 PowerShell 中执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```

构建脚本会在当前用户范围自动安装缺失的 `ps2exe` 模块，并生成：

```text
dist\AMacQ配置编辑器.exe
```

## 使用步骤

1. 启动程序后，点击“选择文件...”。
2. 依次选择按键配置 Lua 文件和灵敏度配置 Lua 文件；两个角色必须选择不同的文件。
3. 在左侧选择鼠标型号和需要编辑的枪械。
4. 调整按键、灵敏度、触发方式和灵敏度增幅激活键。
5. 点击“应用”写入修改。

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `AMacQGuiEditor.ps1` | PowerShell + WPF 图形编辑器主程序源码 |
| `Build-Release.ps1` | 使用 ps2exe 打包 EXE 的开发者构建脚本 |
| `dist\AMacQ配置编辑器.exe` | 构建生成的单文件 Windows 图形程序（发布文件） |
| `启动AMacQ配置界面.vbs` | 旧版兼容启动器；EXE 发布版本不需要使用它 |

### 应用图标

`assets\AMacQ.ico` 是 EXE、应用窗口、任务栏和 Alt+Tab 共用的图标资源。构建时会将该图标嵌入 `AMacQ配置编辑器.exe`，发布 EXE 时不需要额外分发 ICO 或 PNG 文件。

如需基于新的 PNG 更新图标，将 PNG 保存到项目中，然后执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Convert-Icon.ps1 -InputPath .\assets\AMacQ-source.png
```

完成转换后重新运行 `Build-Release.ps1`。

## 注意事项

- 请只选择确认可以编辑的配置文件；修改前建议自行备份原文件。
- 灵敏度数值支持负数，最多保留两位小数。
- 如果配置中的变量格式不符合工具预期，加载或保存时会显示错误信息；请先检查所选文件是否正确。
