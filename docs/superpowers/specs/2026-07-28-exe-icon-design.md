# AMacQ EXE 图标集成设计

## 目标

使用用户提供的绿色 AMacQ 标志，为发布的 Windows EXE 与运行中的 WPF 窗口提供一致的应用图标。

## 图标资源

将用户提供的 PNG 转换为 `assets\AMacQ.ico`。ICO 保留透明背景，并包含 16、20、24、32、40、48、64、128 和 256 像素图像，以适配资源管理器、桌面快捷方式、任务栏、Alt+Tab 与高 DPI 显示。

PNG 仅作为转换输入，不作为发布时依赖；项目中受版本控制的图标资源是生成的 ICO 文件。

## EXE 打包

`Build-Release.ps1` 在调用 `Invoke-ps2exe` 时传递 `-iconFile` 和 `assets\AMacQ.ico` 的绝对路径。ps2exe 会将图标嵌入 `dist\AMacQ配置编辑器.exe`。

因此发布时仍只需要分发 EXE；用户无需将 ICO 或 PNG 放在 EXE 同一目录。

构建脚本在开始打包前确认图标文件存在，缺失时输出明确错误，而不是生成没有品牌图标的 EXE。

## 应用窗口

WPF Window 的 XAML 设置 `Icon` 属性，指向打包后 EXE 内嵌并可由应用程序集 URI 访问的同一 ICO 资源。窗口左上角、任务栏和 Alt+Tab 显示与 EXE 文件一致的图标。

在直接运行 `AMacQGuiEditor.ps1` 的开发场景中，窗口图标从项目根目录的 `assets\AMacQ.ico` 加载；在打包 EXE 中，则从嵌入的图标资源加载，不依赖外部文件。

## 验证

- 自动测试确认 ICO 存在并包含要求的图像尺寸。
- 自动测试确认构建脚本验证图标存在，并将其通过 `-iconFile` 传递给 ps2exe。
- 自动测试确认窗口 XAML 设置了图标。
- 重新生成 EXE，检查其文件图标资源存在。
- 启动 EXE，确认主窗口创建并在任务栏与 Alt+Tab 中显示绿色 AMacQ 图标。

## 非目标

- 不改变现有配置读取、编辑或写入逻辑。
- 不将 PNG 作为 EXE 运行时外部依赖。
- 不修改已忽略的 `dist` 目录的版本控制策略。
