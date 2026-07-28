# 深海极光主题设计

**日期：** 2026-07-28  
**状态：** 已确认，待审阅  
**范围：** `AMacQGuiEditor.ps1` 的 WPF 窗口主题资源、控件模板和运行时生成的字段卡片

## 目标

将 AMacQ 配置编辑器升级为干净、有层次的“深海极光”主题，并将所有可见主题色集中为一套可维护的资源变量。后续调整主题时，只修改 `Window.Resources` 中的命名颜色资源，不需要搜索或修改各个控件、模板或 PowerShell 运行时代码中的颜色字面量。

不改变窗口布局、配置读取/写入逻辑、EXE 打包流程或已验证的应用图标。

## 主题资源架构

主题资源在 `Window.Resources` 中按以下顺序定义。

### 颜色调色板

所有具体颜色使用命名 `Color` 资源定义，包括：

- 深海背景、侧栏、内容区、面板、输入框和弹出层的渐变起止色；
- 极光效果的三档透明色；
- 青色至靛蓝强调渐变的起止色；
- 主文字、正文文字、次级文字、列表文字与强调色上的文字；
- 控件边框、焦点边框、分隔线、面板外框与标题栏分隔线；
- 控件悬停、按下、滚动条轨道、滚动条滑块和滑块悬停色；
- 关闭按钮的危险悬停色；
- 所有八秒渐变动画的目标色。

资源键按语义命名，例如 `TextPrimaryColor`、`SurfaceInputStartColor`、`BorderFocusColor`、`AccentCyanColor`、`DangerCloseHoverColor` 和 `AnimationPopupEndColor`。颜色值只允许出现在这一区域。

### 语义画刷和渐变画刷

所有 XAML 控件和模板只引用语义化的画刷资源，不直接引用十六进制值、`White` 或其他可见颜色字面量。例如：

- `PrimaryTextBrush`、`BodyTextBrush`、`SecondaryTextBrush`、`ListItemTextBrush`、`AccentForegroundBrush`；
- `ControlBorderBrush`、`FocusBorderBrush`、`DividerBrush`、`PanelOutlineBrush`、`TitleBarDividerBrush`；
- `ControlHoverBrush`、`ControlPressedBrush`、`ScrollTrackBrush`、`ScrollThumbBrush`、`ScrollThumbHoverBrush`、`WindowCloseHoverBrush`；
- `AppBackgroundBrush`、`AuroraGlowBrush`、`AccentGradientBrush`、`SidebarSurfaceBrush`、`ContentSurfaceBrush`、`PanelSurfaceBrush`、`InputSurfaceBrush` 和 `PopupSurfaceBrush`。

语义画刷由上述命名 `Color` 资源构成。主题化的 XAML 引用统一使用资源键；需要随资源替换而更新的模板属性优先使用 `DynamicResource`。

布局透明性仍使用 WPF 原生 `Transparent`，因为它表示结构性透出，而非可配置的视觉颜色。

### PowerShell 运行时代码和动画

`Build-FieldCards` 通过 `$FieldCardsGrid.FindResource(...)` 获取与 XAML 相同的语义画刷，包括文字、输入表面、边框、分隔线和焦点/插入符画刷。该函数不得再使用 `BrushConverter.ConvertFromString()` 或硬编码的主题色。

`Start-AnimatedBackground` 通过 `$Window.Resources[...]` 读取命名动画目标 `Color` 资源，再创建 `ColorAnimation`。动画周期保持八秒，且仅为背景、面板、输入框与弹出层提供低幅度明暗变化。

## 视觉系统

### 窗口主体

- 根窗口背景由左上偏蓝紫的 `#263B68` 平滑过渡至右下深蓝黑 `#090E20`。
- 左上叠加低透明度青蓝极光，并向窗口中部自然消散；高光不得形成明显色块或降低文字可读性。
- 标题栏沿用根背景，使自定义标题栏与主体连续；底部细分隔线使用命名的低强调分隔画刷。
- 保留八秒、低幅度背景渐变动画，避免闪烁和频繁明暗变化。

### 分区表面

- 侧栏使用更深的半透明靛蓝表面，以区分导航与内容，同时允许主体渐变透出。
- 右侧内容区承接主背景，分组与底部操作区域以低强调深海蓝分隔线区分层次。
- 配置卡片使用深蓝半透明表面与低饱和蓝青外框，保证内容在背景前清晰可辨。
- 不使用扫描线或其他纹理遮罩；必须移除 `ScanlineOverlay` 和 `DrawingBrush`。

### 表单控件与交互

- 输入框和下拉框使用不透明深海蓝表面（约 `#132440` 至 `#0D1B32`）、蓝青边框、悬停表面与焦点边框。
- 下拉弹出层使用比输入框略深的蓝黑表面；普通选项使用可读列表文字，悬停项使用蓝青高亮，选中项使用青色至靛蓝强调渐变和强调前景色。
- 侧栏按钮、枪械列表、标题栏最小化按钮和滚动条使用相同的深海蓝/蓝青交互状态。
- 保存按钮与选中项继续使用强调渐变；强调渐变上的文字使用 `AccentForegroundBrush`。
- 关闭按钮的危险悬停反馈保留，但通过 `WindowCloseHoverBrush` 引用其独立语义色。
- 文本在普通、悬停、选中与禁用状态下保持可读；禁用态可使用局部不透明度降低，但不引入新的颜色字面量。

## 实现边界

- 主要改动集中在 `AMacQGuiEditor.ps1` 的 `Window.Resources`、控件模板、`Build-FieldCards` 和 `Start-AnimatedBackground`。
- 主题资源集中于一个连续的资源区，不在控件模板或 PowerShell 逻辑中散落十六进制主题色。
- 不新增外部依赖、图像资产或运行时服务。
- 不修改窗口布局、业务逻辑、打包脚本或图标资源。

## 验证标准

1. `Window.Resources` 定义命名 `Color` 调色板、动画目标色和全部语义画刷；生产 UI 代码不残留可见主题颜色字面量（`Transparent` 除外）。
2. `Start-AnimatedBackground` 从资源字典读取动画目标色；`Build-FieldCards` 只通过 `FindResource` 获取主题画刷。
3. 窗口主体呈现连续的深海极光渐变，标题栏与主体视觉连贯。
4. 不存在扫描线覆盖层、`ScanlineOverlay` 或 `DrawingBrush` 纹理。
5. 所有下拉框、文本输入框、弹出列表、列表项、滚动条和按钮使用语义主题资源并呈现一致的深海蓝/蓝青交互状态。
6. 危险关闭按钮仍使用独立的红色悬停反馈，强调按钮与选中项仍使用青色至靛蓝渐变与高对比文字。
7. 原有窗口操作、配置编辑、应用图标与 EXE 打包行为不变。
8. 静态测试、XAML 解析测试与完整 PowerShell 测试套件通过；如可用，手工启动窗口确认下拉展开、字段卡片和交互状态。
