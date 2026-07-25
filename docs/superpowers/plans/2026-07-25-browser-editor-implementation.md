# AMacQ 离线浏览器配置编辑器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增一个可双击打开、完全离线的浏览器版 AMacQ Lua 配置编辑器，同时不改变现有 WPF 编辑器和启动器。

**Architecture:** 浏览器版由无依赖的静态 HTML、CSS 和 JavaScript 组成。`app.js` 将配置/Lua/编码/保存逻辑与 DOM 渲染分开，核心函数通过 CommonJS 条件导出供 Node 内建测试器测试；页面端优先使用 File System Access API 写回用户选择的文件，失败或不支持时下载同名修改文件。

**Tech Stack:** HTML5、CSS3、原生 ES2020 JavaScript、Web File API、File System Access API、Node.js 内建 `node:test`/`node:assert`（仅测试）、现有 PowerShell Pester 测试。

## Global Constraints

- 保持 `AMacQGuiEditor.ps1` 与 `启动AMacQ配置界面.vbs` 的代码和行为不变。
- 浏览器入口必须为 `web/index.html`，用户可通过双击以 `file://` 打开。
- 不使用 npm 包、CDN、网络请求、服务器、数据库或构建步骤。
- 仅处理用户明确选择的两个 Lua 文件；不扫描目录、不访问固定路径、不与游戏进程交互。
- 支持 UTF-8、UTF-8 BOM、UTF-16 LE、UTF-16 BE 的读取和按原编码重新输出。
- Edge/Chrome 可写时优先直写；任何不可直写情形必须下载两个保持源文件名的结果文件。
- 数值格式必须为 `^-?(?:\d+(?:\.\d{1,2})?|\.\d{1,2})$`。
- 页面宽屏保持左侧列表、右侧详情；窄屏切换为纵向单列布局，并遵循 `prefers-reduced-motion`。

---

## File Structure

- Create: `web/index.html` — 静态界面、可访问的表单控件、状态区和无 API 时的隐藏文件选择控件。
- Create: `web/styles.css` — 紫蓝深色主题、宽屏/窄屏布局、交互状态与减弱动画支持。
- Create: `web/app.js` — 配置定义、二进制编码、Lua 编辑、应用规则、文件句柄保存、下载回退和 DOM 控制器。
- Create: `tests/browser-editor.test.js` — 不依赖浏览器或第三方包的核心逻辑单元测试。
- Modify: `tests/WeaponListSearch.Tests.ps1` — 仅添加网页文件存在及 WPF 文件未被网页迁移删除的回归断言。

## Task 1: 创建可测试的浏览器核心逻辑

**Files:**
- Create: `web/app.js`
- Test: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: 无。
- Produces: `decodeLuaFile(bytes: Uint8Array): { content: string, encoding: 'utf-8'|'utf-8-bom'|'utf-16le'|'utf-16be' }`、`encodeLuaFile(content: string, encoding: string): Uint8Array`、`getLuaAssignments(content: string): Array<{name:string,value:string}>`、`getPrimaryWeapons(content: string): string[]`、`getLuaStringValue(content: string, variableName: string): string|null`、`setLuaValue(content: string, variableName: string, newValue: string): string`、`setLuaStringValue(content: string, variableName: string, newValue: string): string`、`validateDecimalValue(value: string): string`、`applyConfiguration(model: ConfigModel, selection: EditSelection): ConfigModel`。

- [ ] **Step 1: 写出编码、Lua 读写与冲突清理的失败测试**

创建 `tests/browser-editor.test.js`：

```js
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const {
  decodeLuaFile,
  encodeLuaFile,
  getPrimaryWeapons,
  getLuaStringValue,
  setLuaValue,
  setLuaStringValue,
  validateDecimalValue,
  applyConfiguration,
} = require('../web/app.js');

const keyBindings = [
  'press = 1',
  'modeswitch = "scrolllock"',
  'AK_qq1156777787 = 4',
  'AK_qq1156777787_second = 0',
  'AK_Third = 5',
  'M4_qq1156777787 = 4',
  'M4_qq1156777787_second = 3',
  'M4_Third = 0',
].join('\n');

const sensitivity = [
  'AK_qq1156777787_X = 1.25',
  'AK_qq1156777787_Y = -0.5',
  'AK_qq1156777787_add_X = .25',
  'AK_qq1156777787_add_Y = 0',
  'M4_qq1156777787_X = 2',
  'M4_qq1156777787_Y = 2',
].join('\n');

test('round trips supported encodings and preserves BOM choice', () => {
  for (const encoding of ['utf-8', 'utf-8-bom', 'utf-16le', 'utf-16be']) {
    const bytes = encodeLuaFile('枪械 = 1', encoding);
    const decoded = decodeLuaFile(bytes);
    assert.equal(decoded.content, '枪械 = 1');
    assert.equal(decoded.encoding, encoding);
  }
});

test('discovers weapons from configured key binding suffixes', () => {
  assert.deepEqual(getPrimaryWeapons(keyBindings), ['AK', 'M4']);
});

test('updates numeric and quoted Lua assignments without changing surrounding text', () => {
  assert.match(setLuaValue(keyBindings, 'AK_Third', '0'), /AK_Third = 0/);
  assert.equal(getLuaStringValue(keyBindings, 'modeswitch'), 'scrolllock');
  assert.match(setLuaStringValue(keyBindings, 'modeswitch', 'capslock'), /modeswitch = "capslock"/);
  assert.throws(() => setLuaValue(keyBindings, 'Missing', '1'), /Variable not found/);
});

test('accepts only configured decimal values', () => {
  for (const value of ['0', '-1', '1.25', '-.5', '.25']) assert.equal(validateDecimalValue(value), value);
  for (const value of ['', '1.234', '1.', '--1', 'text']) assert.throws(() => validateDecimalValue(value));
});

test('applies global and selected weapon values then clears conflicting key fields', () => {
  const result = applyConfiguration({
    files: {
      KeyBindings: { content: keyBindings },
      Sensitivity: { content: sensitivity },
    },
  }, {
    weapon: 'AK', press: '3', modeSwitch: 'capslock',
    values: {
      'KeyBindings|qq1156777787': '4',
      'KeyBindings|qq1156777787_second': '3',
      'KeyBindings|Third': '5',
      'Sensitivity|qq1156777787_X': '1.5',
      'Sensitivity|qq1156777787_Y': '-.5',
      'Sensitivity|qq1156777787_add_X': '.25',
      'Sensitivity|qq1156777787_add_Y': '0',
    },
  });
  assert.match(result.files.KeyBindings.content, /press = 3/);
  assert.match(result.files.KeyBindings.content, /modeswitch = "capslock"/);
  assert.match(result.files.KeyBindings.content, /M4_qq1156777787 = 0/);
  assert.match(result.files.KeyBindings.content, /M4_qq1156777787_second = 0/);
  assert.match(result.files.Sensitivity.content, /AK_qq1156777787_X = 1.5/);
});
```

- [ ] **Step 2: 运行测试，确认其失败**

Run: `node --test tests/browser-editor.test.js`

Expected: FAIL，错误包含 `Cannot find module '../web/app.js'`。

- [ ] **Step 3: 实现不依赖 DOM 的配置、编码和 Lua 核心**

创建 `web/app.js`，先写入以下核心部分；后续任务在同一文件追加浏览器控制器：

```js
'use strict';

const TARGET_FILES = ['KeyBindings', 'Sensitivity'];
const VALUE_PATTERN = '-?(?:\\d+(?:\\.\\d{1,2})?|\\.\\d{1,2})';
const DECIMAL_PATTERN = new RegExp(`^${VALUE_PATTERN}$`);
const FIELD_DEFS = [
  { file: 'KeyBindings', suffix: 'qq1156777787', type: 'combo' },
  { file: 'KeyBindings', suffix: 'qq1156777787_second', type: 'combo' },
  { file: 'KeyBindings', suffix: 'Third', type: 'combo' },
  { file: 'Sensitivity', suffix: 'qq1156777787_X', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_Y', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_add_X', type: 'decimal' },
  { file: 'Sensitivity', suffix: 'qq1156777787_add_Y', type: 'decimal' },
];

function decodeLuaFile(bytes) {
  const data = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  if (data.length >= 3 && data[0] === 0xef && data[1] === 0xbb && data[2] === 0xbf) {
    return { content: new TextDecoder('utf-8').decode(data.subarray(3)), encoding: 'utf-8-bom' };
  }
  if (data.length >= 2 && data[0] === 0xff && data[1] === 0xfe) {
    return { content: new TextDecoder('utf-16le').decode(data.subarray(2)), encoding: 'utf-16le' };
  }
  if (data.length >= 2 && data[0] === 0xfe && data[1] === 0xff) {
    const swapped = new Uint8Array(data.length - 2);
    for (let index = 2; index < data.length; index += 2) {
      swapped[index - 2] = data[index + 1];
      swapped[index - 1] = data[index];
    }
    return { content: new TextDecoder('utf-16le').decode(swapped), encoding: 'utf-16be' };
  }
  return { content: new TextDecoder('utf-8').decode(data), encoding: 'utf-8' };
}

function encodeLuaFile(content, encoding) {
  const body = new TextEncoder().encode(content);
  if (encoding === 'utf-8') return body;
  if (encoding === 'utf-8-bom') return Uint8Array.from([0xef, 0xbb, 0xbf, ...body]);
  const utf16 = new Uint8Array(content.length * 2);
  for (let index = 0; index < content.length; index += 1) {
    const code = content.charCodeAt(index);
    utf16[index * 2] = code & 0xff;
    utf16[index * 2 + 1] = code >> 8;
  }
  if (encoding === 'utf-16le') return Uint8Array.from([0xff, 0xfe, ...utf16]);
  if (encoding === 'utf-16be') {
    const result = new Uint8Array(utf16.length + 2);
    result[0] = 0xfe; result[1] = 0xff;
    for (let index = 0; index < utf16.length; index += 2) {
      result[index + 2] = utf16[index + 1]; result[index + 3] = utf16[index];
    }
    return result;
  }
  throw new Error(`Unsupported encoding: ${encoding}`);
}

function getLuaAssignments(content) {
  return [...content.matchAll(new RegExp(`^\\s*(?<name>[A-Za-z0-9_]+)\\s*=\\s*(?<value>${VALUE_PATTERN})`, 'gm'))]
    .map((match) => ({ name: match.groups.name, value: match.groups.value }));
}

function getPrimaryWeapons(content) {
  const suffixes = FIELD_DEFS.filter((field) => field.file === 'KeyBindings')
    .map((field) => field.suffix.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&')).join('|');
  const pattern = new RegExp(`^(?<weapon>[A-Za-z0-9]+)_(?:${suffixes})$`);
  const seen = new Set();
  return getLuaAssignments(content).flatMap(({ name }) => {
    const match = name.match(pattern);
    if (!match || seen.has(match.groups.weapon)) return [];
    seen.add(match.groups.weapon);
    return [match.groups.weapon];
  });
}

function setLuaValue(content, variableName, newValue) {
  const escaped = variableName.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&');
  const pattern = new RegExp(`^(\\s*${escaped}\\s*=\\s*)${VALUE_PATTERN}`, 'm');
  if (!pattern.test(content)) throw new Error(`Variable not found in content: ${variableName}`);
  return content.replace(pattern, `$1${newValue}`);
}

function getLuaStringValue(content, variableName) {
  const escaped = variableName.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&');
  const match = content.match(new RegExp(`^\\s*${escaped}\\s*=\\s*"(?<value>[^"]*)"`, 'm'));
  return match ? match.groups.value : null;
}

function setLuaStringValue(content, variableName, newValue) {
  const escaped = variableName.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&');
  const pattern = new RegExp(`^(\\s*${escaped}\\s*=\\s*)"[^"]*"`, 'm');
  if (!pattern.test(content)) throw new Error(`Variable not found in content: ${variableName}`);
  return content.replace(pattern, `$1"${newValue}"`);
}

function validateDecimalValue(value) {
  if (!DECIMAL_PATTERN.test(value)) throw new Error('请输入数值（支持负数，最多两位小数）。');
  return value;
}

function applyConfiguration(model, selection) {
  const files = Object.fromEntries(TARGET_FILES.map((file) => [file, { ...model.files[file] }]));
  let keyBindings = setLuaValue(files.KeyBindings.content, 'press', selection.press);
  keyBindings = setLuaStringValue(keyBindings, 'modeswitch', selection.modeSwitch);
  for (const field of FIELD_DEFS) {
    const key = `${field.file}|${field.suffix}`;
    const value = selection.values[key];
    const variable = `${selection.weapon}_${field.suffix}`;
    const source = files[field.file].content;
    if (!new RegExp(`^\\s*${variable.replace(/[.*+?^${}()|[\\]\\]/g, '\\$&')}\\s*=`, 'm').test(source)) continue;
    if (field.type === 'decimal') validateDecimalValue(value);
    files[field.file].content = setLuaValue(source, variable, value);
  }
  const keyValues = new Map(FIELD_DEFS.filter((field) => field.file === 'KeyBindings')
    .map((field) => [field.suffix, selection.values[`KeyBindings|${field.suffix}`]])
    .filter(([, value]) => value && value !== '0'));
  for (const { name, value } of getLuaAssignments(files.KeyBindings.content)) {
    const match = name.match(/^(?<weapon>[A-Za-z0-9]+)_(?<suffix>.+)$/);
    if (match && match.groups.weapon !== selection.weapon && value !== '0' && keyValues.get(match.groups.suffix) === value) {
      files.KeyBindings.content = setLuaValue(files.KeyBindings.content, name, '0');
    }
  }
  return { ...model, files };
}

const exported = { TARGET_FILES, FIELD_DEFS, decodeLuaFile, encodeLuaFile, getLuaAssignments, getPrimaryWeapons, getLuaStringValue, setLuaValue, setLuaStringValue, validateDecimalValue, applyConfiguration };
if (typeof module !== 'undefined') module.exports = exported;
```

- [ ] **Step 4: 运行核心测试，确认通过**

Run: `node --test tests/browser-editor.test.js`

Expected: PASS，5 个子测试全部通过。

- [ ] **Step 5: 提交此任务（若工作区随后初始化为 Git 仓库）**

```bash
git add web/app.js tests/browser-editor.test.js
git commit -m "feat: add browser editor core logic"
```

当前工作区不是 Git 仓库；执行此步骤前必须先由用户决定是否初始化仓库，不能自动执行。

## Task 2: 实现离线网页结构与响应式深色主题

**Files:**
- Create: `web/index.html`
- Create: `web/styles.css`
- Modify: `web/app.js`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: `app.js` 的 `TARGET_FILES`、`FIELD_DEFS` 和后续 DOM 初始化函数 `initializeBrowserEditor()`。
- Produces: 所有 DOM 元素的固定 `id`：`choose-files`、`refresh-files`、`key-file-input`、`sensitivity-file-input`、`mouse-model`、`weapon-list`、`press`、`mode-switch`、`field-cards`、`save-mode`、`status`、`apply`。

- [ ] **Step 1: 写出网页资源和旧版 WPF 保留的失败回归测试**

在 `tests/WeaponListSearch.Tests.ps1` 文件末尾追加：

```powershell
Describe 'Browser editor entry point' {
    It 'adds a self-contained offline browser editor without removing WPF entry points' {
        $root = Join-Path $PSScriptRoot '..'

        Test-Path (Join-Path $root 'web\index.html') | Should BeTrue
        Test-Path (Join-Path $root 'web\styles.css') | Should BeTrue
        Test-Path (Join-Path $root 'web\app.js') | Should BeTrue
        Test-Path (Join-Path $root 'AMacQGuiEditor.ps1') | Should BeTrue
        Test-Path (Join-Path $root '启动AMacQ配置界面.vbs') | Should BeTrue

        $html = Get-Content -Raw (Join-Path $root 'web\index.html')
        $html | Should Match 'id="choose-files"'
        $html | Should Match 'id="weapon-list"'
        $html | Should Match 'id="field-cards"'
        $html | Should Match 'src="app.js"'
    }
}
```

- [ ] **Step 2: 运行 Pester 测试确认失败**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1 -Output Detailed"`

Expected: FAIL，`web\index.html` 不存在。

- [ ] **Step 3: 创建语义化页面结构**

创建 `web/index.html`：

```html
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="color-scheme" content="dark">
  <title>AMacQ Configuration Editor</title>
  <link rel="stylesheet" href="styles.css">
</head>
<body>
  <main class="app-shell">
    <aside class="sidebar" aria-label="配置导航">
      <div class="sidebar-header">
        <h1>AMacQ</h1>
        <div class="header-actions">
          <button id="refresh-files" type="button" disabled>刷新</button>
          <button id="choose-files" type="button">选择文件…</button>
        </div>
      </div>
      <label class="control-label" for="mouse-model">鼠标型号</label>
      <select id="mouse-model"></select>
      <p class="control-label weapon-heading">枪械</p>
      <ul id="weapon-list" class="weapon-list" aria-label="枪械列表"></ul>
    </aside>
    <section class="content-panel">
      <header class="content-header">
        <h2 id="selected-weapon">请选择枪械</h2>
        <p>仅编辑所选文件，不上传、不与游戏进程交互</p>
      </header>
      <section class="settings-section" aria-labelledby="global-title">
        <h3 id="global-title">全局设置</h3>
        <div class="global-fields">
          <label>触发方式<select id="press"></select></label>
          <label>灵敏度增幅激活键<select id="mode-switch"></select></label>
        </div>
      </section>
      <section class="details-section" aria-labelledby="details-title">
        <h3 id="details-title">配置详情</h3>
        <div id="field-cards" class="field-cards"></div>
      </section>
      <footer class="action-bar">
        <p id="save-mode" role="status">保存模式：下载后替换</p>
        <p id="status" role="alert" aria-live="polite"></p>
        <button id="apply" class="primary-button" type="button" disabled>应用</button>
      </footer>
    </section>
  </main>
  <input id="key-file-input" type="file" accept=".lua,text/plain" hidden>
  <input id="sensitivity-file-input" type="file" accept=".lua,text/plain" hidden>
  <script src="app.js"></script>
</body>
</html>
```

- [ ] **Step 4: 创建响应式样式**

创建 `web/styles.css`：

```css
:root { color-scheme: dark; font-family: "Segoe UI", system-ui, sans-serif; background: #0b1024; color: #f7f2ff; }
* { box-sizing: border-box; }
body { min-width: 320px; min-height: 100vh; margin: 0; background: #0b1024; }
body::before { position: fixed; z-index: 2; inset: 0; pointer-events: none; opacity: .08; content: ""; background: repeating-linear-gradient(to bottom, #66bdebff 0 1px, transparent 1px 4px); }
.app-shell { display: grid; grid-template-columns: 220px minmax(0, 1fr); min-height: 100vh; background: linear-gradient(135deg, #26345e, #0b1024); }
.sidebar { padding: 20px 14px 16px; border-right: 1px solid #4a3a70; background: linear-gradient(to bottom, #26345e, #182243); }
.sidebar-header { display: flex; align-items: start; justify-content: space-between; gap: 8px; margin-bottom: 16px; }
h1, h2, h3, p { margin-top: 0; } h1 { margin-bottom: 0; font-size: 20px; color: #5dd7ff; } h2 { margin-bottom: 6px; font-size: 26px; } h3, .control-label { color: #bdb3dd; font-size: 13px; font-weight: 600; }
.header-actions { display: flex; gap: 3px; } button, select, input { font: inherit; } button { border: 0; border-radius: 6px; padding: 7px 9px; color: #f7f2ff; background: transparent; cursor: pointer; } button:hover:not(:disabled) { background: #3856b8; } button:active:not(:disabled) { background: #2a4fad; } button:disabled, select:disabled, input:disabled { cursor: not-allowed; opacity: .5; }
select, input { width: 100%; height: 32px; padding: 4px 8px; border: 1px solid #6488c4; border-radius: 5px; color: #f7f2ff; background: #16264c; } .weapon-heading { margin: 18px 6px 7px; } .weapon-list { max-height: calc(100vh - 190px); overflow: auto; margin: 0; padding: 4px; border: 1px solid #6488c4; border-radius: 8px; list-style: none; } .weapon-list button { width: 100%; text-align: left; } .weapon-list button[aria-current="true"] { font-weight: 600; color: white; background: linear-gradient(90deg, #22d3ee, #6366f1); }
.content-panel { display: grid; grid-template-rows: auto auto 1fr auto; min-width: 0; } .content-header, .settings-section, .details-section, .action-bar { padding: 22px 32px; border-bottom: 1px solid #5476af; } .content-header p, #save-mode { margin-bottom: 0; color: #bdb3dd; font-size: 12px; } .global-fields, .field-cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; max-width: 760px; } label { display: grid; gap: 6px; color: #d8d0ef; font-size: 12px; } .details-section { overflow: auto; } .field-group { padding: 0 8px; } .field-group h4 { color: #bdb3dd; font-size: 12px; } .field-row { display: grid; grid-template-columns: 1fr 140px; align-items: center; min-height: 44px; gap: 12px; border-bottom: 1px solid #4a3a70; } .field-row:last-child { border-bottom: 0; } .field-row label { color: #ede7ff; font-size: 13px; } .action-bar { display: flex; align-items: center; justify-content: flex-end; gap: 16px; border-bottom: 0; } #status { flex: 1; margin: 0; color: #bdb3dd; font-size: 12px; } .primary-button { padding: 9px 28px; font-weight: 600; color: white; background: linear-gradient(90deg, #22d3ee, #6366f1); } .primary-button:hover:not(:disabled) { opacity: .88; background: linear-gradient(90deg, #22d3ee, #6366f1); }
@media (max-width: 760px) { .app-shell { grid-template-columns: 1fr; } .sidebar { border-right: 0; border-bottom: 1px solid #4a3a70; } .weapon-list { display: flex; max-height: 108px; gap: 4px; } .weapon-list button { white-space: nowrap; } .content-header, .settings-section, .details-section, .action-bar { padding: 18px; } .global-fields, .field-cards { grid-template-columns: 1fr; } .action-bar { flex-wrap: wrap; } #status { min-width: 100%; order: 3; } }
@media (prefers-reduced-motion: no-preference) { .app-shell { animation: background-shift 8s ease-in-out infinite alternate; } @keyframes background-shift { to { filter: hue-rotate(8deg); } } }
```

- [ ] **Step 5: 在 `web/app.js` 末尾加入 DOM 启动占位调用，使页面加载不报错**

在 `if (typeof module !== 'undefined') module.exports = exported;` 之后追加：

```js
if (typeof document !== 'undefined') {
  document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('status').textContent = '请选择两个 Lua 配置文件开始编辑。';
  });
}
```

- [ ] **Step 6: 运行 Pester 和 Node 测试，确认通过**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1 -Output Detailed"; node --test tests/browser-editor.test.js`

Expected: Pester 全部通过，Node 5 个子测试全部通过。

- [ ] **Step 7: 提交此任务（若已存在 Git 仓库）**

```bash
git add web/index.html web/styles.css web/app.js tests/WeaponListSearch.Tests.ps1
git commit -m "feat: add responsive offline browser editor shell"
```

## Task 3: 连接页面、文件选择、配置加载与编辑控件

**Files:**
- Modify: `web/app.js`
- Modify: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: Task 1 导出的核心函数，Task 2 固定 DOM id。
- Produces: 页面端 `loadSelectedFiles(files: { KeyBindings: File, Sensitivity: File }, handles?: object): Promise<void>`、`renderWeaponFields(): void`、`getSaveCapability(): 'direct'|'download'`；页面状态 `state.model` 存储 `{files, weapons}`。

- [ ] **Step 1: 增加模型构建失败测试**

在 `tests/browser-editor.test.js` 的导入清单中加入 `buildConfigModel`，并在文件末尾添加：

```js
test('builds a model, rejects duplicate file names, and rejects missing weapons', () => {
  const model = buildConfigModel({
    KeyBindings: { name: 'KeyBindings.lua', content: keyBindings, encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: sensitivity, encoding: 'utf-8' },
  });
  assert.deepEqual(model.weapons, ['AK', 'M4']);
  assert.throws(() => buildConfigModel({
    KeyBindings: { name: 'same.lua', content: keyBindings, encoding: 'utf-8' },
    Sensitivity: { name: 'same.lua', content: sensitivity, encoding: 'utf-8' },
  }), /不同的文件/);
  assert.throws(() => buildConfigModel({
    KeyBindings: { name: 'KeyBindings.lua', content: 'press = 1', encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: sensitivity, encoding: 'utf-8' },
  }), /没有识别到枪械/);
});
```

- [ ] **Step 2: 运行 Node 测试确认失败**

Run: `node --test tests/browser-editor.test.js`

Expected: FAIL，错误包含 `buildConfigModel is not a function`。

- [ ] **Step 3: 在 `web/app.js` 中加入模型创建与完整浏览器控制器**

在 `const exported = ...` 之前加入以下代码，并将 `buildConfigModel` 加入 `exported`：

```js
const PRESS_OPTIONS = [{ text: '鼠标左键', value: '1' }, { text: '按住右键 + 鼠标左键', value: '3' }];
const MODE_SWITCH_OPTIONS = [{ text: 'Scroll Lock', value: 'scrolllock' }, { text: 'Caps Lock', value: 'capslock' }, { text: 'Num Lock', value: 'numlock' }];
const MOUSE_PROFILES = {
  '通用双侧键鼠标': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  G102: [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G304 / G305': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G Pro Wireless（GPW）': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5'], ['右侧后退键(7)', '7'], ['右侧前进键(8)', '8']],
  'G Pro X Superlight（GPX）': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  G402: [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G502 Hero': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
  'G502 X': [['左侧后退键(4)', '4'], ['左侧前进键(5)', '5']],
};

function buildConfigModel(files) {
  if (files.KeyBindings.name === files.Sensitivity.name) throw new Error('请为两个配置角色选择不同的文件。');
  const weapons = getPrimaryWeapons(files.KeyBindings.content);
  if (!weapons.length) throw new Error('按键配置文件中没有识别到枪械。');
  if (!getLuaAssignments(files.KeyBindings.content).some(({ name }) => name === 'press')) throw new Error('按键配置文件缺少 press。');
  if (getLuaStringValue(files.KeyBindings.content, 'modeswitch') === null) throw new Error('按键配置文件缺少 modeswitch。');
  return { files, weapons };
}

function populateSelect(select, options, selectedValue) {
  select.replaceChildren(...options.map(({ text, value }) => {
    const option = new Option(text, value, false, value === selectedValue);
    return option;
  }));
}

function initializeBrowserEditor() {
  const elements = Object.fromEntries(['choose-files', 'refresh-files', 'key-file-input', 'sensitivity-file-input', 'mouse-model', 'weapon-list', 'press', 'mode-switch', 'field-cards', 'selected-weapon', 'save-mode', 'status', 'apply'].map((id) => [id, document.getElementById(id)]));
  const state = { model: null, handles: {}, selectedWeapon: null };
  const setStatus = (message, error = false) => { elements.status.textContent = message; elements.status.style.color = error ? '#ff9ca8' : '#bdb3dd'; };
  const getKeyOptions = () => [['无按键(0)', '0'], ...(MOUSE_PROFILES[elements['mouse-model'].value] || MOUSE_PROFILES['通用双侧键鼠标'])];
  const updateSaveMode = () => { const direct = TARGET_FILES.every((file) => state.handles[file]); elements['save-mode'].textContent = `保存模式：${direct ? '可直接写回' : '下载后替换'}`; };
  const selectedFieldValue = (field) => {
    const variable = `${state.selectedWeapon}_${field.suffix}`;
    const source = state.model.files[field.file].content;
    return getLuaAssignments(source).find(({ name }) => name === variable)?.value ?? '';
  };
  const renderFields = () => {
    elements['field-cards'].replaceChildren();
    if (!state.model || !state.selectedWeapon) return;
    for (const [file, title] of [['KeyBindings', '按键'], ['Sensitivity', '灵敏度']]) {
      const group = document.createElement('section'); group.className = 'field-group'; group.innerHTML = `<h4>${title}</h4>`;
      for (const field of FIELD_DEFS.filter((item) => item.file === file)) {
        const value = selectedFieldValue(field); const variable = `${state.selectedWeapon}_${field.suffix}`;
        const row = document.createElement('div'); row.className = 'field-row';
        const label = document.createElement('label'); label.textContent = ({ qq1156777787: '无修饰键', qq1156777787_second: '按住 Alt', Third: '按住 Ctrl', qq1156777787_X: '灵敏度 X', qq1156777787_Y: '灵敏度 Y', qq1156777787_add_X: '灵敏度 增幅 X', qq1156777787_add_Y: '灵敏度 增幅 Y' })[field.suffix];
        const control = document.createElement(field.type === 'combo' ? 'select' : 'input'); control.dataset.fieldKey = `${field.file}|${field.suffix}`; control.disabled = value === '';
        if (field.type === 'combo') populateSelect(control, getKeyOptions().map(([text, optionValue]) => ({ text, value: optionValue })), value); else { control.type = 'text'; control.value = value; control.inputMode = 'decimal'; }
        row.append(label, control); group.append(row);
      }
      elements['field-cards'].append(group);
    }
  };
  const renderWeapons = () => {
    elements['weapon-list'].replaceChildren();
    for (const weapon of state.model.weapons) {
      const button = document.createElement('button'); button.type = 'button'; button.textContent = weapon; button.setAttribute('aria-current', String(weapon === state.selectedWeapon));
      button.addEventListener('click', () => { state.selectedWeapon = weapon; elements['selected-weapon'].textContent = `枪械：${weapon}`; renderWeapons(); renderFields(); });
      elements['weapon-list'].append(document.createElement('li')).append(button);
    }
  };
  const loadFiles = async (files, handles = {}) => {
    try {
      const entries = {};
      for (const file of TARGET_FILES) { const decoded = decodeLuaFile(new Uint8Array(await files[file].arrayBuffer())); entries[file] = { name: files[file].name, content: decoded.content, encoding: decoded.encoding }; }
      state.model = buildConfigModel(entries); state.handles = handles; state.selectedWeapon = state.model.weapons[0];
      const keyContent = state.model.files.KeyBindings.content;
      populateSelect(elements.press, PRESS_OPTIONS, getLuaAssignments(keyContent).find(({ name }) => name === 'press').value);
      populateSelect(elements['mode-switch'], MODE_SWITCH_OPTIONS, getLuaStringValue(keyContent, 'modeswitch'));
      renderWeapons(); renderFields(); updateSaveMode(); elements.apply.disabled = false; elements['refresh-files'].disabled = false; setStatus('配置已加载。');
    } catch (error) { state.model = null; elements.apply.disabled = true; elements['refresh-files'].disabled = true; setStatus(`加载配置失败：${error.message}`, true); }
  };
  const chooseWithInputs = () => { elements['key-file-input'].value = ''; elements['sensitivity-file-input'].value = ''; elements['key-file-input'].click(); };
  elements['key-file-input'].addEventListener('change', () => elements['sensitivity-file-input'].click());
  elements['sensitivity-file-input'].addEventListener('change', () => { if (elements['key-file-input'].files[0] && elements['sensitivity-file-input'].files[0]) loadFiles({ KeyBindings: elements['key-file-input'].files[0], Sensitivity: elements['sensitivity-file-input'].files[0] }); });
  elements['choose-files'].addEventListener('click', async () => {
    if (!window.showOpenFilePicker) return chooseWithInputs();
    try { const [keyHandle] = await window.showOpenFilePicker({ multiple: false, types: [{ description: 'Lua 文件', accept: { 'text/plain': ['.lua'] } }] }); const [sensitivityHandle] = await window.showOpenFilePicker({ multiple: false, types: [{ description: 'Lua 文件', accept: { 'text/plain': ['.lua'] } }] }); await loadFiles({ KeyBindings: await keyHandle.getFile(), Sensitivity: await sensitivityHandle.getFile() }, { KeyBindings: keyHandle, Sensitivity: sensitivityHandle }); } catch (error) { if (error.name !== 'AbortError') setStatus(`选择文件失败：${error.message}`, true); }
  });
  elements['refresh-files'].addEventListener('click', async () => { if (TARGET_FILES.every((file) => state.handles[file])) await loadFiles({ KeyBindings: await state.handles.KeyBindings.getFile(), Sensitivity: await state.handles.Sensitivity.getFile() }, state.handles); else if (elements['key-file-input'].files[0]) await loadFiles({ KeyBindings: elements['key-file-input'].files[0], Sensitivity: elements['sensitivity-file-input'].files[0] }); else chooseWithInputs(); });
  elements['mouse-model'].addEventListener('change', renderFields);
  populateSelect(elements['mouse-model'], Object.keys(MOUSE_PROFILES).map((value) => ({ text: value, value })), '通用双侧键鼠标');
  return { state, elements, loadFiles, setStatus, updateSaveMode };
}
```

替换现有 `DOMContentLoaded` 代码为：

```js
if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', initializeBrowserEditor);
```

- [ ] **Step 4: 运行 Node 测试确认模型逻辑通过**

Run: `node --test tests/browser-editor.test.js`

Expected: PASS，6 个子测试全部通过。

- [ ] **Step 5: 手工验证加载和编辑控件**

Run: `start "" "web\index.html"`

Expected: 默认浏览器打开本地页面；选择两个不同的有效 Lua 文件后，显示枪械、全局下拉框和当前枪械字段；切换鼠标型号会刷新按键下拉选项；字段不存在时控件不可编辑。

- [ ] **Step 6: 提交此任务（若已存在 Git 仓库）**

```bash
git add web/app.js tests/browser-editor.test.js
git commit -m "feat: load local Lua files in browser editor"
```

## Task 4: 实现安全保存、下载回退与应用操作

**Files:**
- Modify: `web/app.js`
- Modify: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: `applyConfiguration(model, selection)`、浏览器控制器返回的 `state` 和 `elements`。
- Produces: `canWriteDirectly(handles: object): Promise<boolean>`、`saveModel(model: ConfigModel, handles: object): Promise<'direct'|'download'>`；“应用”按钮变为完成编辑、直写或下载的端到端操作。

- [ ] **Step 1: 增加保存模式判定的失败测试**

在 `tests/browser-editor.test.js` 的导入清单中加入 `shouldUseDirectSave`，并在末尾添加：

```js
test('uses direct saving only when both file handles exist', () => {
  assert.equal(shouldUseDirectSave({ KeyBindings: {}, Sensitivity: {} }), true);
  assert.equal(shouldUseDirectSave({ KeyBindings: {} }), false);
  assert.equal(shouldUseDirectSave({}), false);
});
```

- [ ] **Step 2: 运行 Node 测试确认失败**

Run: `node --test tests/browser-editor.test.js`

Expected: FAIL，错误包含 `shouldUseDirectSave is not a function`。

- [ ] **Step 3: 在 `web/app.js` 中实现保存函数**

在 `initializeBrowserEditor` 之前添加以下函数，并将 `shouldUseDirectSave` 加入 `exported`：

```js
function shouldUseDirectSave(handles) {
  return TARGET_FILES.every((file) => handles[file]);
}

async function canWriteDirectly(handles) {
  if (!shouldUseDirectSave(handles)) return false;
  for (const file of TARGET_FILES) {
    const permission = await handles[file].queryPermission({ mode: 'readwrite' });
    if (permission === 'granted') continue;
    if (await handles[file].requestPermission({ mode: 'readwrite' }) !== 'granted') return false;
  }
  return true;
}

function downloadFile(file) {
  const blob = new Blob([encodeLuaFile(file.content, file.encoding)], { type: 'application/octet-stream' });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob); link.download = file.name; link.click();
  window.setTimeout(() => URL.revokeObjectURL(link.href), 0);
}

async function saveModel(model, handles) {
  if (await canWriteDirectly(handles)) {
    for (const file of TARGET_FILES) {
      const writable = await handles[file].createWritable();
      await writable.write(encodeLuaFile(model.files[file].content, model.files[file].encoding));
      await writable.close();
    }
    return 'direct';
  }
  for (const file of TARGET_FILES) downloadFile(model.files[file]);
  return 'download';
}
```

在 `initializeBrowserEditor()` 内、`return { state, ... }` 之前添加应用监听器：

```js
  elements.apply.addEventListener('click', async () => {
    try {
      if (!state.model || !state.selectedWeapon) return;
      const values = Object.fromEntries([...elements['field-cards'].querySelectorAll('[data-field-key]')]
        .filter((control) => !control.disabled)
        .map((control) => [control.dataset.fieldKey, control.value]));
      const next = applyConfiguration(state.model, {
        weapon: state.selectedWeapon,
        press: elements.press.value,
        modeSwitch: elements['mode-switch'].value,
        values,
      });
      state.model = next;
      const mode = await saveModel(next, state.handles);
      setStatus(mode === 'direct' ? '应用成功：已写回原文件。' : '已下载修改文件，请替换原文件。');
      updateSaveMode(); renderFields();
    } catch (error) {
      try {
        for (const file of TARGET_FILES) downloadFile(state.model.files[file]);
        setStatus(`无法写回原文件：${error.message}。已下载修改文件，请替换原文件。`, true);
      } catch (downloadError) {
        setStatus(`保存失败：${error.message}；下载回退失败：${downloadError.message}`, true);
      }
    }
  });
```

- [ ] **Step 4: 运行 Node 测试确认通过**

Run: `node --test tests/browser-editor.test.js`

Expected: PASS，7 个子测试全部通过。

- [ ] **Step 5: 手工验证直写与下载回退**

Run: `start "" "web\index.html"`

Expected:

1. 在支持 File System Access API 的 Edge/Chrome 中选择文件并授权后，页面显示“保存模式：可直接写回”，点击“应用”后显示“已写回原文件”。
2. 拒绝写入权限或在不支持 API 的浏览器中选择文件后，页面显示“保存模式：下载后替换”，点击“应用”会下载两个文件，下载名与源文件名相同。
3. 故意输入 `1.234` 后点击“应用”时不保存，显示数值格式错误，页面中的输入不丢失。

- [ ] **Step 6: 提交此任务（若已存在 Git 仓库）**

```bash
git add web/app.js tests/browser-editor.test.js
git commit -m "feat: save browser edits locally or download fallback"
```

## Task 5: 全量回归与交付检查

**Files:**
- Modify: 无（仅在发现失败时按对应任务修复）。
- Test: `tests/browser-editor.test.js`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: Tasks 1–4 的完整实现。
- Produces: 已验证的离线网页编辑器；现有 WPF 文件保持原样。

- [ ] **Step 1: 运行网页核心测试**

Run: `node --test tests/browser-editor.test.js`

Expected: PASS，所有子测试通过。

- [ ] **Step 2: 运行 WPF 现有回归测试**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1 -Output Detailed"`

Expected: PASS，所有 Pester 断言通过；现有 WPF 样式、本地安全边界和手动双文件选择断言仍通过。

- [ ] **Step 3: 检查静态资源不包含外部依赖或网络访问**

Run: `grep -RInE "https?://|fetch\(|XMLHttpRequest|WebSocket|import .*from" web`

Expected: 无输出，退出码为 1；这表示网页没有网络资源或网络调用。

- [ ] **Step 4: 手工浏览器验收**

Run: `start "" "web\index.html"`

Expected: 页面可从 `file://` 打开；宽屏侧栏布局、窄屏单列布局、文件选择、刷新、鼠标型号、枪械切换、字段禁用、冲突清理、编码保存、直写/下载回退和状态消息均可用。

- [ ] **Step 5: 检查 WPF 入口未被修改或删除**

Run: `powershell -NoProfile -Command "Get-Item .\AMacQGuiEditor.ps1, .\启动AMacQ配置界面.vbs | Select-Object Name,Length,LastWriteTime | Format-Table -AutoSize"`

Expected: 两个文件都存在；实施中未编辑它们时其修改时间不应变化。

- [ ] **Step 6: 提交验证结果对应的最终改动（若已存在 Git 仓库）**

```bash
git status --short
git add web tests
git commit -m "feat: add offline browser configuration editor"
```

当前工作区不是 Git 仓库；不能执行提交，需在用户明确要求初始化 Git 后才可进行。

## Self-Review

- **规格覆盖：** Tasks 1 和 3 处理字段、Lua、编码、枪械解析、全局设置和冲突清理；Task 2 处理双击入口、视觉风格与响应式页面；Task 4 处理授权直写、下载同名回退和状态反馈；Task 5 验证无网络、WPF 保留、手工浏览器流程与自动测试。
- **占位检查：** 本计划不含 TBD/TODO 或“自行处理”等未定义实现步骤。所有新增函数、DOM id、命令和测试预期均已明确。
- **一致性检查：** `ConfigModel.files[KeyBindings|Sensitivity]`、`FIELD_DEFS`、`applyConfiguration`、`buildConfigModel`、`saveModel`、`shouldUseDirectSave` 在任务间使用相同名称和参数含义。
