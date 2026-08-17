# AMacQ 配置编辑器

用于编辑 AMacQ Lua 配置文件的 Windows WPF 桌面应用。程序仅读取和修改用户在界面中主动选择的本地配置文件，不会上传文件，也不会与游戏进程交互。

## 运行环境

- Windows 10 x64（建议已完成常规系统更新）
- .NET Framework 4.8 或更高版本的 .NET Framework 4.x

发布版不依赖 .NET 10 Desktop Runtime，也不需要附带 DLL、ICO 或 ZIP 文件。目标电脑仍可能因网吧安全软件或系统策略阻止未知 EXE；此类限制需要由电脑管理方处理。

## 技术栈

- .NET Framework 4.8
- WPF
- C#
- `System.IO.Compression.ZipArchive`（读取并解压内嵌 ZIP 资源）

## 开发环境

- Windows 10 或更高版本
- .NET 10 SDK（用于 SDK 风格项目的构建）
- .NET Framework 4.8 Developer Pack / Reference Assemblies
- Visual Studio：安装“使用 .NET 的桌面开发”工作负载

双击根目录的 `AMacQ配置编辑器.sln`，即可在 Visual Studio 中打开解决方案。

## 单 EXE 发布

在项目根目录运行：

```powershell
.\Build-WpfRelease.cmd
```

脚本以 Release 配置构建，并校验发布目录中只保留一个 EXE：

```text
dist\net48\AMacQConfigEditor.exe
```

请复制该 EXE 到目标电脑运行。不要使用 Visual Studio 的“发布”页替代此脚本；脚本会移除 .NET Framework 自动生成的 `.exe.config`，以保证单文件分发。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `AMacQ配置编辑器.sln` | Visual Studio 解决方案 |
| `src/AMacQConfigEditor` | WPF 主项目 |
| `src/AMacQConfigEditor/MainWindow.xaml` | 窗口布局、主题资源与控件样式 |
| `src/AMacQConfigEditor/Services` | Lua 读取、文件编码、原子写入与资源部署服务 |
| `src/AMacQConfigEditor/ViewModels` | 配置编辑状态与界面数据绑定 |
| `assets/AMacQ.ico` | EXE 与自绘标题栏使用的内嵌图标 |
| `Build-WpfRelease.cmd` | 双击构建入口 |
| `author-tools` | 作者私钥与授权签发工具（不对外分发） |

## 本地构建

```powershell
dotnet restore .\AMacQ配置编辑器.sln
dotnet build .\AMacQ配置编辑器.sln -c Release --no-restore
```

## 离线授权

首次运行时，程序会显示机器码。将该机器码发送给授权方，由授权方生成许可证 JSON 文件；在授权窗口中导入该文件后才能进入主界面。

作者在本机保存 `author-tools\AMacQLicense.private.xml` 私钥文件，绝不能提交、发送或打包此文件。双击根目录的 `启动授权签发工具.cmd` 可打开签发界面。

也可以使用无需安装的离线网页签发器：用 Edge 或 Chrome 打开 `tools\AMacQLicenseGenerator\离线授权签发.html`，选择私钥 XML、粘贴客户机器码并生成许可证 JSON。网页不会上传任何数据；私钥仅在本机浏览器内用于签名。该 HTML 同样属于作者工具，不能发送给客户。

### GitHub Pages 发布

仓库根目录的 `index.html` 会跳转至离线网页签发器。推送到 GitHub 后，在仓库 **Settings → Pages** 中选择 **Deploy from a branch**、`main` 分支和 `/(root)` 目录，保存后通过 `https://<GitHub 用户名>.github.io/<仓库名>/` 打开。私钥文件绝不能提交到仓库。

```powershell
tools\AMacQLicenseGenerator\bin\Release\net48\AMacQLicenseGenerator.exe .\AMacQLicense.private.xml D:\licenses\user-license.json <机器码> perpetual
```

签发到期许可证（示例到期日为 2027-08-12）：

```powershell
tools\AMacQLicenseGenerator\bin\Release\net48\AMacQLicenseGenerator.exe .\AMacQLicense.private.xml D:\licenses\user-license.json <机器码> expires 2027-08-12
```

工具会生成你指定路径的许可证 JSON 文件。将该文件发送给对应机器的用户；用户换机或重装系统后，收集新的机器码并重新签发。纯离线许可证无法远程撤销已发出的许可证文件。

## 主要功能

- 分别选择按键配置与灵敏度配置两个 Lua 文件
- 读取枪械配置，并根据鼠标型号提供对应按键选项
- 显示每个枪械已有的按键绑定摘要与颜色标记
- 编辑 X/Y 灵敏度和灵敏度增幅数值
- 设置触发方式与灵敏度增幅激活键
- 保存时保留原文件编码、尽量保留 Lua 内容格式，并通过临时文件原子替换写入
- 敏感度输入仅允许非负整数或最多两位小数，支持方向键以 `0.01` 调整
- 启动时随机选择固定科技主题
- 将内嵌 ZIP 资源解压到 `C:\`；若目标一级目录已存在则跳过，不覆盖已有内容

## 配置文件使用

1. 启动程序，选择“选择文件”。
2. 依次选择按键配置 Lua 文件和灵敏度配置 Lua 文件；两个文件必须不同。
3. 在左侧选择鼠标型号与枪械。
4. 修改按键、灵敏度或全局设置。
5. 点击“应用”保存修改。

修改前请自行备份配置文件。若 Lua 变量格式不符合当前解析规则，程序会提示加载或保存错误。
