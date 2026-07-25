# AMacQ 网页侧栏宽度调整 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将宽屏网页侧栏从 220px 加宽到 280px，使标题、刷新和选择文件按钮保持同一行且不再被挤压换行。

**Architecture:** 只改变 `web/styles.css` 中宽屏 `.app-shell` 的第一列固定宽度。现有 HTML、按钮布局、JavaScript 和小屏 `max-width: 760px` 单列媒体查询均不变。

**Tech Stack:** CSS3、Pester 3、Node.js 内建测试器。

## Global Constraints

- 仅修改 `web/styles.css` 和 `tests/WeaponListSearch.Tests.ps1`。
- 宽屏 `.app-shell` 的网格第一列必须为 `280px`。
- 不调整 `.sidebar-header`、`.header-actions`、按钮文本、HTML 或 JavaScript。
- 保持 `@media (max-width: 760px)` 下 `.app-shell { grid-template-columns: 1fr; }` 不变。
- 不引入依赖、资源、网络请求或构建步骤。

---

## File Structure

- Modify: `tests/WeaponListSearch.Tests.ps1` — 添加侧栏宽度与小屏布局回归断言。
- Modify: `web/styles.css` — 将宽屏侧栏网格宽度改为 280px。

### Task 1: 调整宽屏侧栏宽度并验证

**Files:**
- Modify: `tests/WeaponListSearch.Tests.ps1`
- Modify: `web/styles.css`
- Test: `tests/WeaponListSearch.Tests.ps1`
- Test: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: `web/styles.css` 既有 `.app-shell` 宽屏规则与 `@media (max-width: 760px)` 响应式规则。
- Produces: 宽屏 `.app-shell` 使用 `grid-template-columns: 280px minmax(0, 1fr)`；小屏仍切换为单列。

- [ ] **Step 1: 写出失败的侧栏宽度回归断言**

在 `tests/WeaponListSearch.Tests.ps1` 文件末尾添加：

```powershell
Describe 'Browser sidebar width' {
    It 'widens the desktop sidebar while preserving the narrow-screen single column layout' {
        $root = Join-Path $PSScriptRoot '..'
        $styles = Get-Content -Raw (Join-Path $root 'web\styles.css')

        $styles | Should Match '\.app-shell\s*\{[\s\S]*?grid-template-columns: 280px minmax\(0, 1fr\);'
        $styles | Should Match '@media \(max-width: 760px\)\s*\{[\s\S]*?\.app-shell\s*\{\s*grid-template-columns: 1fr;\s*\}'
    }
}
```

- [ ] **Step 2: 运行 Pester，确认新断言失败**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"`

Expected: 现有测试通过，`Browser sidebar width` 失败，指出没有找到 `280px` 宽屏网格定义。

- [ ] **Step 3: 修改 `web/styles.css` 的宽屏网格定义**

将：

```css
.app-shell {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
```

替换为：

```css
.app-shell {
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
```

- [ ] **Step 4: 运行完整自动验证**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"; node --test tests/browser-editor.test.js; node --check web/app.js`

Expected: Pester 全部通过；Node 显示 9 个通过、0 个失败；语法检查无输出且退出码为 0。

- [ ] **Step 5: 手工确认按钮不换行**

Run: `start "" "启动AMacQ网页配置界面.vbs"`

Expected: 网页服务模式打开后，标题、刷新与“选择文件…”按钮仍在同一行；两个按钮文本不再拆成多行；缩小窗口到 760px 以下时页面仍为单列布局。

- [ ] **Step 6: 提交此任务（仅在用户已将目录初始化为 Git 仓库时）**

```bash
git add web/styles.css tests/WeaponListSearch.Tests.ps1
git commit -m "fix: widen browser sidebar"
```

当前工作区不是 Git 仓库，不能执行提交；不得自动初始化仓库。

## Self-Review

- **规格覆盖：** 唯一实现任务将宽屏侧栏固定改为 280px，并以静态断言锁定宽屏与小屏规则，同时运行 Pester、Node 和语法检查。
- **占位检查：** 包含完整断言、精确 CSS 替换内容、验证命令和预期结果，不包含未定义步骤。
- **一致性检查：** 断言目标 `280px minmax(0, 1fr)` 与 CSS 变更一致；媒体查询断言与现有 `max-width: 760px` 规则一致。
