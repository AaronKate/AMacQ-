# AMacQ 配置编辑器

用于编辑 AMacQ 压枪宏 Lua 配置文件的 Windows WPF 桌面应用。程序仅读取和修改本机的 Lua 配置文件，不会上传任何数据，也不会与游戏进程交互。

## 运行环境

- Windows 10 x64 或更高版本（建议已完成常规系统更新）
- .NET Framework 4.8 或更高版本的 .NET Framework 4.x
- 需以管理员身份运行：程序启动时会检查权限，非管理员状态下会弹窗提示并尝试以管理员身份重启

发布版为单个 EXE，不依赖 .NET 10 Desktop Runtime，也不需要附带 DLL、ICO 或 ZIP 文件。目标电脑仍可能因网吧安全软件或系统策略阻止未知 EXE，此类限制需要由电脑管理方处理。

## 技术栈

- .NET Framework 4.8（`net48`）
- WPF（`UseWPF`）
- C#（`LangVersion=latest`，启用可空引用类型）
- `System.Windows.Forms`（托盘图标与右键菜单）
- `System.IO.Compression.ZipArchive`（读取并解压内嵌 ZIP 资源）
- RSA-SHA256（离线许可证签名校验）

## 开发环境

- Windows 10 或更高版本
- .NET 10 SDK（用于 SDK 风格项目的构建）
- .NET Framework 4.8 Developer Pack / Reference Assemblies
- Visual Studio：安装“使用 .NET 的桌面开发”工作负载

双击根目录的 `AMacQ配置编辑器.sln`，即可在 Visual Studio 中打开解决方案。

## 构建与发布

在项目根目录双击 `Build-WpfRelease.cmd`（或直接运行 `scripts\Build-RenamedRelease.ps1`），脚本会：

1. 清理 `dist\net48`；
2. 以 Release 配置构建验证版程序，重命名为 `AMacQ配置编辑器-验证版.exe`；
3. 以 `AuthorEdition=true` 构建作者自用版，重命名为 `AMacQ配置编辑器-作者版.exe`；
4. 删除自动生成的 `.exe.config`；
5. 使用 Obfuscar 对两个 EXE 进行混淆。

产物位于：

```text
dist\net48\AMacQ配置编辑器-验证版.exe
dist\net48\AMacQ配置编辑器-作者版.exe
```

请把需要的 EXE 复制到目标电脑运行。不要使用 Visual Studio 的“发布”页替代此脚本；脚本会移除 .NET Framework 自动生成的 `.exe.config`，以保证单文件分发。

`scripts\Build-WpfRelease.ps1` 是另一个更完整的脚本：除上述步骤外，还会额外构建授权签发工具并复制到 `author-tools\AMacQLicenseGenerator.exe`。

本地不带混淆的构建：

```powershell
dotnet restore .\AMacQ配置编辑器.sln
dotnet build .\AMacQ配置编辑器.sln -c Release --no-restore
```

## 回归测试

改动配置读写、按键槽位、搜索排序等逻辑后，请跑一遍常驻回归测试：

```powershell
dotnet run --project .\tests\AMacQConfigEditor.Tests\AMacQConfigEditor.Tests.csproj
```

它是一个自包含的控制台程序（不依赖测试框架），逐项打印 `PASS`/`FAIL`，全部通过时退出码为 0。覆盖的都是历史上真实出过问题的地方：

- Lua 解析：枪械识别（含 `AKS-74` 这类带连字符的名字）、全局变量不被误认成枪械、取值；
- Lua 写回：批量写入与逐条替换等价、同槽位冲突清零、`0` 表示解绑、字符串写入的引号行为；
- 搜索排序：完全匹配优先于前缀、多关键词、中文名与按键摘要命中；
- 按键槽位：主键 / Alt / Ctrl 的后缀映射与冲突检测；
- 键值换算：侧键 `XBUTTON1/2` 与主键区、小键盘数字；
- 快速切换窗口：筛选、自动选中、列表显示中文名、`Enter` 确认、`Esc` 取消；
- 托盘菜单结构：枪械列表为"骨架 + 按需展开"，展开后补齐三个按键位子菜单且可重复展开；
- 配置文件的禁用/恢复状态机：关闭程序后禁用、启动时恢复，两份并存时也仍然生效（以较新的活动文件为准）；
- 编码识别：GBK/ANSI 配置按原编码读出并原样写回（不被偷偷改成 UTF-8），UTF-8 BOM 与 UTF-16 正确处理；
- **键值为 6/7/8/9 的枪械切换后仍能正常保存且原值不被清空**（曾因下拉框表示不了该值而保存失败）；
- 非法值在写盘前被校验拦下，并给出具体字段提示。

## 项目结构

| 路径 | 说明 |
| --- | --- |
| `AMacQ配置编辑器.sln` | Visual Studio 解决方案 |
| `src/AMacQConfigEditor` | WPF 主项目 |
| `src/AMacQConfigEditor/App.xaml.cs` | 启动流程：主题、权限、授权校验 |
| `src/AMacQConfigEditor/MainWindow.xaml` | 主界面布局与主题资源 |
| `src/AMacQConfigEditor/MainWindow.xaml.cs` | 主窗口：字段、构造、生命周期、部署与授权提示 |
| `src/AMacQConfigEditor/MainWindow.TrayMenu.cs` | 托盘图标与菜单（含按键位子菜单的懒加载） |
| `src/AMacQConfigEditor/MainWindow.HotKeys.cs` | 全局快捷键注册与消息处理、灵敏度提示 |
| `src/AMacQConfigEditor/MainWindow.Weapons.cs` | 枪械列表、搜索过滤、字段卡片、保存与输入校验 |
| `src/AMacQConfigEditor/MainWindow.QuickSwitch.cs` | 快速切换窗口入口、后台等待按键绑定 |
| `src/AMacQConfigEditor/QuickSwitchWindow.xaml(.cs)` | `Ctrl + Alt + M` 唤起的纯键盘枪械切换窗口 |
| `src/AMacQConfigEditor/HelpWindow.xaml(.cs)` | 内置使用说明窗口（含图文教程与截图查看器） |
| `src/AMacQConfigEditor/LicenseWindow.xaml(.cs)` | 机器码显示与许可证导入窗口 |
| `src/AMacQConfigEditor/AdminPromptWindow.xaml(.cs)` | 管理员权限提示窗口 |
| `src/AMacQConfigEditor/SensitivityOverlayWindow.xaml(.cs)` | 屏幕右上角悬浮提示窗口（灵敏度与绑键结果） |
| `src/AMacQConfigEditor/Services` | Lua 读写、编码、原子写入、按键监听、资源部署、主题、G HUB 启动等服务 |
| `src/AMacQConfigEditor/ViewModels` | 配置编辑状态、界面数据绑定与搜索过滤规则 |
| `src/AMacQConfigEditor/Licensing` | 机器码、许可证文档、验签与存储 |
| `src/AMacQConfigEditor/Resources` | 内嵌资源包（ZIP）与使用说明截图 |
| `tests/AMacQConfigEditor.Tests` | 常驻回归测试（控制台程序，见上节） |
| `assets/AMacQ.ico` | EXE 与自绘标题栏使用的内嵌图标 |
| `scripts` | 构建、重命名发布与混淆脚本 |
| `tools/AMacQLicenseGenerator` | 授权签发工具（WPF 界面 / 命令行 / 离线网页） |
| `author-tools` | 作者私钥与授权签发工具（不对外分发） |

## 主要功能

### 一键资源部署

- 点击侧栏「资源部署状态」卡片中的一键部署按钮，即可将内嵌资源包解压到本机：`C:\ProgramData\Microsoft\Windows\Caches\<随机目录>\`。
- 首次部署会生成随机目录并写入安装记录，后续启动复用同一目录；旧版本遗留在桌面或 C 盘根目录的启动脚本会在启动时清理。
- 解压完成后会在桌面生成启动脚本 `GHUB - Sorin 25.1 S11-1.lua`，并在资源管理器中选中该文件，同时尝试自动拉起已安装的罗技 G HUB。
- 部署前会检测是否安装罗技 G HUB：未安装时弹窗提供「下载」（打开官方安装页）和「知道了」（仍继续解压资源包）两个选项。
- 部署过程带进度对话框，显示文件计数、百分比与当前处理的文件，并给出成功或失败结果。
- 程序运行期间，安装目录中的两个配置文件会被临时重命名禁用（启动时恢复、退出时禁用），授权过期时同样会禁用。

### 配置加载与解析

- 启动后自动从部署目录加载按键配置 `sorinkg.lua` 与灵敏度配置 `sorinxs.lua`，无需手动选择文件。
- `LuaConfigService` 按 `变量名 = 值` 的赋值格式解析配置，提取枪械列表、按键绑定、灵敏度数值，以及全局的 `press`（触发方式）与 `modeswitch`（灵敏度增幅激活键）。
- 每把枪械的内部标识通过 `WeaponNameMapper` 映射为对外显示的名称。

### 按键编辑

- 根据所选鼠标型号提供对应的按键选项：无按键(0)、左侧后退键(4)、左侧前进键(5)；GPW/GPX 额外提供右侧后退键(7)、右侧前进键(8)。
- 支持三种绑定：主键（无修饰）、Alt 键、Ctrl 键。
- 修改绑定时会自动清除冲突：同一按键值若已被其它枪械占用，则把冲突项置为 0。
- 左侧枪械列表会显示每把枪已有的绑定摘要（例如 `4 · Alt+5`）。

### 灵敏度编辑

- 可编辑基础灵敏度 X / Y，以及灵敏度增幅 X / Y。
- 灵敏度 Y 控制垂直压枪幅度，X 为水平补偿，增幅用于弹匣后半段后坐力变大的情况。
- 输入只允许非负整数或最多两位小数，并支持用方向键以 `0.01` 步进调整。

### 全局快捷键

- 程序运行时会注册一组全局快捷键（需按住 Ctrl 与 Alt），按下一次触发一次，长按不会重复触发，主窗口最小化到托盘后依然可用。
- 全局灵敏度快捷键只修改当前选中枪械的基础 X / Y 值，不修改增幅值；连续调整会合并为一次写入，停止操作约 2 秒后自动保存到灵敏度配置文件。
- 当快捷键已被其它程序占用时，托盘区会提示注册失败。

### 托盘菜单与桌面快捷菜单

- 最小化主窗口或点击最小化按钮会隐藏到托盘，双击托盘图标可还原窗口。
- 托盘右键菜单包含：打开主窗口、当前枪械状态、可直接调整当前枪械 X / Y 的「灵敏度」子菜单（每项 ±0.05，连续调整时菜单保持打开）、按分类展开的枪械列表，以及退出。
- 枪械列表按突击步枪 / 冲锋枪 / 轻机枪 / 射手步枪 / 其它分组，选中枪械后可直接修改 Alt / Ctrl 绑定，修改即保存。
- 全局快捷键 `Ctrl + Alt + M` 会打开「快速切换窗口」：可直接用键盘切换当前枪械——输入枪械名或内部代码筛选（支持中文名，如“野牛”），`↑`/`↓` 移动，`Enter` 切换并保存。窗口在鼠标所在屏幕居中弹出，并提示当前枪械与即将切换到的枪械。
- 按 `Enter` 后**窗口立即消失并进入后台等待**：这时直接在鼠标上按一下要绑定的侧键（侧键 4 = 后退键、侧键 5 = 前进键）即完成绑定，屏幕右上角会用悬浮提示反馈结果（如 `MK4 · 主键 已绑定 侧键5`）；也可以直接敲数字键 `0/4/5/6/7/8/9` 指定。等待约 15 秒没有任何输入会自动结束，不影响正在进行的操作。
- 想绑到别的槽位就用 `Alt + Enter`（Alt 槽位）或 `Ctrl + Enter`（Ctrl 槽位）。只想换枪不想绑键，选好枪后不按任何键即可。
- 若该“槽位 + 按键”已被其它枪械占用，写入时会自动清空对方，并在悬浮提示中标注枪名。绑定同样可以在托盘菜单中修改。
- 托盘菜单与子菜单使用自绘渲染（圆角、渐变、主题色与勾选样式），并监听外部点击以自动收起。

### 灵敏度悬浮提示

- 使用全局灵敏度快捷键调整后，程序会在当前屏幕右上角短暂显示 `枪械名 | x: 数值  y: 数值`，约 2 秒后自动淡出。
- 该悬浮提示不会抢走当前窗口焦点；托盘菜单、切换枪械和主窗口内的手动调整不会显示该提示。

### 保存机制

- `AtomicFileWriter` 通过“写入临时文件 + 原子替换”保存，降低写入中断导致文件损坏的风险。
- `FileEncodingService` 会保留原文件的编码（含 BOM），`LuaConfigService` 只替换目标变量的值，尽量保留原有的 Lua 内容与格式。
- 保存后按钮会短暂显示「应用成功」再复位。

### 界面主题

- 启动时从内置的多套固定科技主题中随机选择一套（如 Deep Ocean、Quantum Violet、Aurora Cyan、Lava Red 等），并通过启动参数在管理员提权重启后保持一致。
- 界面包含自绘标题栏、终端网格背景、渐变光晕，以及自定义的按钮、输入框、滚动条与动画样式。

### 内置使用说明

- 点击标题栏的「?」可打开使用说明窗口，内容涵盖罗技 G HUB 安装、脚本导入图文步骤、按键与快捷键说明、压枪调节方法、使用须知与常见问题。
- 说明中的截图可点击放大，并支持拖拽平移与滚轮缩放（1～4 倍）。

## 程序快捷键

程序运行时会注册以下全局快捷键，需按住 Ctrl 与 Alt：

| 快捷键 | 功能 |
| --- | --- |
| `Ctrl + Alt + 左方向键` | 当前枪械基础灵敏度 X −0.01 |
| `Ctrl + Alt + 右方向键` | 当前枪械基础灵敏度 X +0.01 |
| `Ctrl + Alt + 下方向键` | 当前枪械基础灵敏度 Y −0.01 |
| `Ctrl + Alt + 上方向键` | 当前枪械基础灵敏度 Y +0.01 |
| `Ctrl + Alt + -` | 当前枪械基础灵敏度 Y −0.05 |
| `Ctrl + Alt + =` | 当前枪械基础灵敏度 Y +0.05 |
| `Ctrl + Alt + M` | 打开快速切换窗口（键盘筛选 + `Enter` 切换当前枪械） |

## 离线授权

首次运行时，程序会显示机器码。将该机器码发送给授权方，由授权方生成许可证 JSON 文件；在授权窗口中导入该文件后才能进入主界面。

- 授权窗口支持一键复制机器码，并校验许可证的签名、授权模式（永久 / 到期）、机器码绑定与到期时间。
- 若许可证无效或不属于本机，导入会被拒绝并提示原因。
- 许可证已过期的，程序会删除本地许可证并禁用运行期配置文件，需重新签发。
- 主界面标题栏会显示授权状态（“永久授权”或“授权至 yyyy-MM-dd”）。

作者在本机保存 `author-tools\AMacQLicense.private.xml` 私钥文件，绝不能提交、发送或打包此文件。双击根目录的 `启动授权签发工具.cmd` 可打开签发界面。

也可以使用无需安装的离线网页签发器：用 Edge 或 Chrome 打开 `tools\AMacQLicenseGenerator\离线授权签发.html`，选择私钥 XML、粘贴客户机器码并生成许可证 JSON。网页不会上传任何数据；私钥仅在本机浏览器内用于签名。该 HTML 同样属于作者工具，不能发送给客户。

### 命令行签发

```powershell
tools\AMacQLicenseGenerator\bin\Release\net48\AMacQLicenseGenerator.exe .\AMacQLicense.private.xml D:\licenses\user-license.json <机器码> perpetual
```

签发到期许可证（示例到期日为 2027-08-12）：

```powershell
tools\AMacQLicenseGenerator\bin\Release\net48\AMacQLicenseGenerator.exe .\AMacQLicense.private.xml D:\licenses\user-license.json <机器码> expires 2027-08-12
```

工具会生成你指定路径的许可证 JSON 文件。将该文件发送给对应机器的用户；用户换机或重装系统后，收集新的机器码并重新签发。纯离线许可证无法远程撤销已发出的许可证文件。

### GitHub Pages 发布

仓库根目录的 `index.html` 会跳转至离线网页签发器。推送到 GitHub 后，在仓库 **Settings → Pages** 中选择 **Deploy from a branch**、`main` 分支和 `/(root)` 目录，保存后通过 `https://<GitHub 用户名>.github.io/<仓库名>/` 打开。私钥文件绝不能提交到仓库。

## 配置文件使用

1. 启动程序并完成离线授权。
2. 点击「一键部署」部署资源包，程序会自动加载配置并生成桌面启动脚本。
3. 按说明窗口的指引，把桌面脚本导入罗技 G HUB 并保存运行。
4. 在左侧选择鼠标型号与枪械。
5. 在“全局设置”和“配置详情”中修改触发方式、增幅激活键、按键与灵敏度。
6. 点击“应用”保存修改，或使用全局快捷键 / 托盘菜单实时微调。

修改前请自行备份配置文件。若 Lua 变量格式不符合当前解析规则，程序会提示加载或保存错误。