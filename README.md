# AMacQ 配置编辑器

用于编辑 AMacQ Lua 配置文件的 Windows 桌面项目。当前主程序为 .NET 10 WPF 应用，项目仅读取和修改用户在界面中主动选择的本地配置文件；不会上传文件，也不会与游戏进程交互。

## 技术栈

- .NET 10
- WPF
- C#（启用可空引用类型与隐式全局 using）
- Windows x64 自包含单文件发布

## 开发环境

- Windows 10 或更高版本
- .NET 10 SDK
- Visual Studio：安装“使用 .NET 的桌面开发”工作负载，并确保已安装 .NET 10 SDK

双击根目录的 `AMacQ配置编辑器.sln`，即可在 Visual Studio 中打开解决方案。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `AMacQ配置编辑器.sln` | Visual Studio 解决方案 |
| `src/AMacQConfigEditor` | WPF 主项目 |
| `src/AMacQConfigEditor/MainWindow.xaml` | 窗口布局、主题资源及全窗口极光背景动画 |
| `src/AMacQConfigEditor/Services` | Lua 读取、文件编码与原子写入服务 |
| `src/AMacQConfigEditor/ViewModels` | 配置编辑状态与界面数据绑定 |
| `assets/AMacQ.ico` | 应用、任务栏和窗口图标 |
| `Build-WpfRelease.ps1` | Windows x64 自包含单文件发布脚本 |

## 本地构建

在项目根目录执行：

```powershell
dotnet restore .\AMacQ配置编辑器.sln
dotnet build .\AMacQ配置编辑器.sln -c Release --no-restore
```

## 发布单文件 EXE

在项目根目录执行：

```powershell
.\Build-WpfRelease.ps1
```

脚本会发布 Windows x64 的自包含单文件程序，输出到：

```text
dist\wpf\AMacQConfigEditor.exe
```

也可以在 Visual Studio 中右键 WPF 项目，选择“发布”，再使用 `FolderProfile` 配置。该配置已启用自包含、单文件和原生库自解压选项。

## 主要功能

- 分别选择按键配置与灵敏度配置两个 Lua 文件
- 读取枪械配置，并根据鼠标型号提供对应按键选项
- 显示每个枪械已有的按键绑定摘要与颜色标记
- 编辑 X/Y 灵敏度和灵敏度增幅数值
- 设置触发方式与灵敏度增幅激活键
- 保存时保留原文件编码、尽量保留 Lua 内容格式，并通过临时文件原子替换写入
- 敏感度输入仅允许非负整数或最多两位小数，支持方向键以 `0.01` 调整
- 深色主题全窗口背景包含纯 XAML 的 22 秒极光渐变往返动画；动画只作用于背景层，不影响编辑和保存逻辑

## 配置文件使用

1. 启动程序，选择“选择文件”。
2. 依次选择按键配置 Lua 文件和灵敏度配置 Lua 文件；两个文件必须不同。
3. 在左侧选择鼠标型号与枪械。
4. 修改按键、灵敏度或全局设置。
5. 点击“应用”保存修改。

修改前请自行备份配置文件。若 Lua 变量格式不符合当前解析规则，程序会提示加载或保存错误。
