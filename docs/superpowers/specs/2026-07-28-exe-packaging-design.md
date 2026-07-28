# AMacQ 配置编辑器 EXE 打包设计

## 目标

将 PowerShell + WPF 图形编辑器提供为一个可双击启动、不会显示 PowerShell 控制台窗口的 Windows `.exe` 发布文件，同时保留现有 PowerShell 脚本作为唯一可维护源码。

## 方案

采用 `ps2exe` 将 `AMacQGuiEditor.ps1` 打包为 `dist\AMacQ配置编辑器.exe`。

- `AMacQGuiEditor.ps1` 继续承载全部应用逻辑。
- 新增根目录构建脚本 `Build-Release.ps1`，集中执行依赖检查和打包。
- 打包产物指定为无控制台窗口的 GUI 应用。
- 构建脚本在写入前仅清理其自身生成的同名 `.exe`，防止发布目录中遗留旧版本。
- 现有 `启动AMacQ配置界面.vbs` 暂时保留，不再作为 README 推荐的启动方式；只有在 EXE 经人工验证后才考虑删除。

## 构建流程

1. 开发者在项目根目录运行 `Build-Release.ps1`。
2. 脚本检测 `ps2exe` 是否可用；缺失时在当前用户范围安装该模块。
3. 脚本确保 `dist` 目录存在，并移除已有的 `dist\AMacQ配置编辑器.exe`。
4. 脚本调用 `ps2exe`，以 `AMacQGuiEditor.ps1` 为输入，生成 `dist\AMacQ配置编辑器.exe`，并启用无控制台窗口模式。
5. 构建脚本验证目标 `.exe` 已生成；失败时将 PowerShell 错误原样返回并以非零状态结束。

## 用户体验

发布时只需分发 `AMacQ配置编辑器.exe`。最终用户双击该文件即可启动编辑器；界面、配置文件选择、读取和保存行为与现有脚本版本保持一致。

目标机器仍须是 Windows，并具备该 WPF 应用运行所需的系统组件。应用不会修改任何配置文件，除非用户在界面中选择文件并点击“应用”。

## 文档更新

README 的“启动”部分改为优先说明双击 `AMacQ配置编辑器.exe`。开发说明中补充构建命令、构建产物位置与 `ps2exe` 的自动安装行为。文件说明增加构建脚本和 EXE 产物，启动器 VBS 标记为旧兼容启动方式。

## 验证

- 对构建脚本进行 PowerShell 语法检查。
- 运行构建脚本并确认 `dist\AMacQ配置编辑器.exe` 生成。
- 检查 EXE 为非空 Windows 可执行文件。
- 手动双击或启动 EXE，确认 WPF 主窗口可打开且没有可见 PowerShell 控制台。

## 非目标

- 不重写现有 PowerShell/WPF 应用为 C#。
- 不将 Lua 配置文件打包进 EXE。
- 本次不删除现有 VBS 启动器。
