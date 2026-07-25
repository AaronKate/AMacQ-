# AMacQ 浏览器版磨砂玻璃主题 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将浏览器版配置编辑器改为可降级的深蓝紫磨砂玻璃主题，不改变任何配置编辑功能。

**Architecture:** 仅调整 `web/styles.css` 的颜色变量、背景层、玻璃面板与控件表面。CSS 使用 `@supports (backdrop-filter: blur(1px))` 为支持的浏览器增加真实背景模糊；基础半透明颜色、边框与阴影始终存在，确保不支持模糊的浏览器仍可读可用。

**Tech Stack:** CSS3、`backdrop-filter`、`@supports`、现有 Pester 和 Node.js 内建测试器。

## Global Constraints

- 仅修改 `web/styles.css` 和 `tests/WeaponListSearch.Tests.ps1`。
- 不修改 `web/index.html`、`web/app.js`、`AMacQGuiEditor.ps1` 或 `.vbs` 启动器。
- 不改变网页结构、JavaScript、文件选择、文件保存或响应式断点。
- 不引入 npm 包、CDN、网络请求、图像资源或构建步骤。
- 页面使用深蓝紫渐变光晕、半透明深色表面、低透明白蓝描边和柔和阴影。
- 支持 `backdrop-filter` 时启用模糊；不支持时必须保持可读、可操作的半透明降级外观。
- 保留青蓝至靛紫的当前枪械与应用按钮强调色，扫描线透明度不得高于 `0.04`。

---

## File Structure

- Modify: `web/styles.css` — 定义磨砂背景、面板、控件、交互态及浏览器降级规则。
- Modify: `tests/WeaponListSearch.Tests.ps1` — 对磨砂主题关键 CSS 规则增加静态回归断言。

### Task 1: 添加磨砂主题静态回归测试

**Files:**
- Modify: `tests/WeaponListSearch.Tests.ps1`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: `web/styles.css` 的静态 CSS 文本。
- Produces: Pester 的 `Browser frosted glass styling` 测试块，断言后续任务需要保留 `rgba` 半透明面板、`backdrop-filter` 支持块、控件玻璃表面与扫描线低透明度。

- [ ] **Step 1: 写出磨砂主题的失败断言**

在 `tests/WeaponListSearch.Tests.ps1` 文件末尾添加：

```powershell
Describe 'Browser frosted glass styling' {
    It 'uses layered translucent glass with a readable fallback' {
        $root = Join-Path $PSScriptRoot '..'
        $styles = Get-Content -Raw (Join-Path $root 'web\styles.css')

        $styles | Should Match 'radial-gradient\('
        $styles | Should Match '\.sidebar[\s\S]*?background: rgba\('
        $styles | Should Match '\.content-panel[\s\S]*?background: rgba\('
        $styles | Should Match 'select, input[\s\S]*?background: rgba\('
        $styles | Should Match '@supports \(backdrop-filter: blur\(1px\)\)'
        $styles | Should Match 'backdrop-filter: blur\('
        $styles | Should Match 'body::before[\s\S]*?opacity: \.04'
        $styles | Should Match 'box-shadow:'
    }
}
```

- [ ] **Step 2: 运行 Pester，确认新断言失败**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"`

Expected: 现有测试通过，`Browser frosted glass styling` 失败，失败信息指出找不到 `radial-gradient(` 或 `@supports (backdrop-filter: blur(1px))`。

- [ ] **Step 3: 提交此任务（仅在用户已将目录初始化为 Git 仓库时）**

```bash
git add tests/WeaponListSearch.Tests.ps1
git commit -m "test: define browser frosted glass styling"
```

当前工作区不是 Git 仓库，不能执行提交；不得自动初始化仓库。

### Task 2: 实现深蓝紫磨砂玻璃样式和降级规则

**Files:**
- Modify: `web/styles.css`
- Test: `tests/WeaponListSearch.Tests.ps1`
- Test: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: Task 1 的静态样式断言；`web/index.html` 既有选择器 `.app-shell`、`.sidebar`、`.content-panel`、`.settings-section`、`.details-section`、`.action-bar`、`.weapon-list`、`select`、`input`、`.primary-button`。
- Produces: 支持浏览器中的模糊玻璃表面与非支持浏览器的半透明降级外观；现有媒体查询和控件选择器保持可用。

- [ ] **Step 1: 用以下完整内容替换 `web/styles.css`**

```css
:root {
  color-scheme: dark;
  font-family: "Segoe UI", system-ui, sans-serif;
  background: #0b1024;
  color: #f7f2ff;
  --glass-border: rgba(193, 225, 255, .22);
  --glass-surface: rgba(13, 24, 61, .58);
  --glass-strong: rgba(9, 18, 49, .72);
  --glass-control: rgba(8, 20, 53, .66);
  --glass-shadow: rgba(2, 8, 29, .34);
}

* { box-sizing: border-box; }

body {
  min-width: 320px;
  min-height: 100vh;
  margin: 0;
  background:
    radial-gradient(circle at 8% 8%, rgba(75, 150, 255, .31), transparent 34%),
    radial-gradient(circle at 88% 18%, rgba(128, 82, 245, .26), transparent 38%),
    radial-gradient(circle at 52% 95%, rgba(25, 209, 230, .16), transparent 44%),
    #0b1024;
}

body::before {
  position: fixed;
  z-index: 2;
  inset: 0;
  pointer-events: none;
  opacity: .04;
  content: "";
  background: repeating-linear-gradient(to bottom, #b5eaff 0 1px, transparent 1px 4px);
}

.app-shell {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
  min-height: 100vh;
  background: rgba(6, 13, 39, .16);
}

.sidebar {
  padding: 20px 14px 16px;
  border-right: 1px solid var(--glass-border);
  background: rgba(19, 35, 81, .54);
  box-shadow: 14px 0 34px var(--glass-shadow);
}

.sidebar-header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 16px;
}

h1, h2, h3, p { margin-top: 0; }
h1 { margin-bottom: 0; font-size: 20px; color: #7ee8ff; text-shadow: 0 0 18px rgba(93, 215, 255, .28); }
h2 { margin-bottom: 6px; font-size: 26px; }
h3, .control-label { color: #c9c4e8; font-size: 13px; font-weight: 600; }

.header-actions { display: flex; gap: 3px; }
button, select, input { font: inherit; }

button {
  border: 1px solid transparent;
  border-radius: 7px;
  padding: 7px 9px;
  color: #f7f2ff;
  background: rgba(255, 255, 255, .03);
  cursor: pointer;
}

button:hover:not(:disabled) { border-color: rgba(152, 219, 255, .25); background: rgba(83, 117, 215, .35); }
button:active:not(:disabled) { background: rgba(53, 91, 185, .54); }
button:disabled, select:disabled, input:disabled { cursor: not-allowed; opacity: .5; }

select, input {
  width: 100%;
  height: 32px;
  padding: 4px 8px;
  border: 1px solid var(--glass-border);
  border-radius: 6px;
  color: #f7f2ff;
  background: rgba(8, 20, 53, .66);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, .06);
}

.weapon-heading { margin: 18px 6px 7px; }
.weapon-list {
  max-height: calc(100vh - 190px);
  overflow: auto;
  margin: 0;
  padding: 4px;
  border: 1px solid var(--glass-border);
  border-radius: 9px;
  background: rgba(6, 17, 47, .38);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, .05);
  list-style: none;
}

.weapon-list button { width: 100%; text-align: left; }
.weapon-list button[aria-current="true"] {
  border-color: rgba(183, 241, 255, .42);
  font-weight: 600;
  color: white;
  background: linear-gradient(90deg, rgba(34, 211, 238, .84), rgba(99, 102, 241, .88));
  box-shadow: 0 7px 18px rgba(34, 112, 220, .22);
}

.content-panel {
  display: grid;
  grid-template-rows: auto auto 1fr auto;
  min-width: 0;
  background: rgba(11, 20, 56, .42);
}

.content-header, .settings-section, .details-section, .action-bar {
  padding: 22px 32px;
  border-bottom: 1px solid var(--glass-border);
  background: rgba(18, 31, 74, .3);
}

.content-header p, #save-mode { margin-bottom: 0; color: #c2bee1; font-size: 12px; }
.global-fields, .field-cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; max-width: 760px; }

label { display: grid; gap: 6px; color: #ddd8f2; font-size: 12px; }
.details-section { overflow: auto; }
.field-group {
  padding: 12px;
  border: 1px solid rgba(193, 225, 255, .12);
  border-radius: 12px;
  background: rgba(12, 25, 65, .24);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, .04);
}
.field-group h4 { color: #c9c4e8; font-size: 12px; }
.field-row { display: grid; grid-template-columns: 1fr 140px; align-items: center; min-height: 44px; gap: 12px; border-bottom: 1px solid rgba(193, 225, 255, .12); }
.field-row:last-child { border-bottom: 0; }
.field-row label { color: #f0ecff; font-size: 13px; }

.action-bar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 16px;
  border-bottom: 0;
  box-shadow: 0 -10px 30px rgba(2, 8, 29, .12);
}
#status { flex: 1; margin: 0; color: #c2bee1; font-size: 12px; }
.primary-button {
  padding: 9px 28px;
  border-color: rgba(199, 246, 255, .34);
  font-weight: 600;
  color: white;
  background: linear-gradient(90deg, rgba(34, 211, 238, .9), rgba(99, 102, 241, .93));
  box-shadow: 0 10px 24px rgba(30, 102, 220, .28);
}
.primary-button:hover:not(:disabled) { opacity: .9; background: linear-gradient(90deg, rgba(34, 211, 238, .9), rgba(99, 102, 241, .93)); }

@supports (backdrop-filter: blur(1px)) {
  .sidebar, .content-panel, .content-header, .settings-section, .details-section, .action-bar, .weapon-list, .field-group, select, input {
    backdrop-filter: blur(18px) saturate(125%);
  }
}

@media (max-width: 760px) {
  .app-shell { grid-template-columns: 1fr; }
  .sidebar { border-right: 0; border-bottom: 1px solid var(--glass-border); }
  .weapon-list { display: flex; max-height: 108px; gap: 4px; }
  .weapon-list button { white-space: nowrap; }
  .content-header, .settings-section, .details-section, .action-bar { padding: 18px; }
  .global-fields, .field-cards { grid-template-columns: 1fr; }
  .action-bar { flex-wrap: wrap; }
  #status { min-width: 100%; order: 3; }
}

@media (prefers-reduced-motion: no-preference) {
  .app-shell { animation: background-shift 8s ease-in-out infinite alternate; }
  @keyframes background-shift { to { filter: hue-rotate(8deg); } }
}
```

- [ ] **Step 2: 运行 CSS 回归、网页逻辑和脚本语法验证**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"; node --test tests/browser-editor.test.js; node --check web/app.js`

Expected: Pester 所有断言通过；Node 显示 7 个通过、0 个失败；语法检查无输出且退出码为 0。

- [ ] **Step 3: 手工检查浏览器视觉与降级**

Run: `start "" "web\index.html"`

Expected: 浏览器从 `file://` 打开网页；左栏、右侧内容、字段组、列表和控件均呈现半透明磨砂层级；标题、标签和输入值清晰可读；缩小窗口后仍为单列布局。开发者工具禁用 `backdrop-filter` 后，半透明背景、边框和文字对比度仍存在。

- [ ] **Step 4: 提交此任务（仅在用户已将目录初始化为 Git 仓库时）**

```bash
git add web/styles.css tests/WeaponListSearch.Tests.ps1
git commit -m "feat: apply frosted glass browser theme"
```

当前工作区不是 Git 仓库，不能执行提交；不得自动初始化仓库。

## Self-Review

- **规格覆盖：** Task 1 锁定多层光晕、半透明面板、控件表面、模糊支持和扫描线要求；Task 2 实现这些 CSS 规则，并验证静态断言、网页逻辑回归、脚本语法和手工视觉降级。
- **占位检查：** 计划提供了完整测试代码、完整 CSS 内容、精确文件路径和验证命令，没有未定义的后续实现步骤。
- **一致性检查：** 所有任务只引用现有 HTML 内的 CSS 选择器；`@supports (backdrop-filter: blur(1px))` 在断言和实现中保持完全一致；扫描线目标透明度均为 `.04`。
