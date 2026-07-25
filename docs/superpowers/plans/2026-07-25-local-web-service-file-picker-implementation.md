# AMacQ 本机网页服务与 C 盘文件选择 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 提供由 PowerShell 本机服务启动的浏览器版，使文件选择框每次严格从 `C:\` 打开，并可直接将网页编辑结果原子写回用户所选的 Lua 文件。

**Architecture:** 新增 PowerShell `HttpListener` 服务，只绑定 `127.0.0.1` 随机可用端口，向浏览器提供固定的网页资源和 API。服务端独占 Windows 文件对话框、选中文件路径、原始编码和原子写入；网页只通过本机会话 API 请求选择、保存、状态和停止服务，并在服务不可用时保留纯网页下载模式。

**Tech Stack:** Windows PowerShell、.NET `HttpListener`、Windows Forms `OpenFileDialog`、原生 HTML/CSS/JavaScript、Node.js 内建测试器、Pester 3。

## Global Constraints

- 新服务只监听 `127.0.0.1`，不得监听 `0.0.0.0`、`localhost` 前缀、局域网或公网 IP。
- 两个 `OpenFileDialog` 都必须设定 `InitialDirectory = 'C:\'`。
- 只能保存用户在当前服务会话中明确选取的两个文件；`/api/apply` 不接受客户端路径。
- 文件写入必须使用临时文件和替换方式，并沿用 UTF-8、UTF-8 BOM、UTF-16 LE、UTF-16 BE 原编码。
- 服务仅静态提供 `web/index.html`、`web/styles.css`、`web/app.js`，禁止由 URL 映射任意磁盘路径。
- 服务在 15 分钟无请求后自动停止；`POST /api/shutdown` 返回后停止。
- 保留 `AMacQGuiEditor.ps1`、现有 WPF `.vbs` 启动器和直接双击 `web/index.html` 的下载保存功能。
- 不使用 npm 包、CDN、网络请求、数据库或游戏进程交互。

---

## File Structure

- Create: `AMacQWebEditorServer.ps1` — 回环 HTTP 服务、C 盘系统选文件、会话受限读写、静态资源和生命周期。
- Create: `启动AMacQ网页配置界面.vbs` — 启动服务、等待端口文件、打开浏览器、展示启动错误。
- Modify: `web/index.html` — 服务连接状态和退出网页服务按钮。
- Modify: `web/app.js` — 服务 API 客户端、服务模式选择/保存、不可用时纯网页回退。
- Modify: `web/styles.css` — 服务状态与退出按钮保持磨砂主题。
- Modify: `tests/WeaponListSearch.Tests.ps1` — PowerShell 服务和新启动器的静态安全/行为断言。
- Modify: `tests/browser-editor.test.js` — 服务状态 URL 与 API 回退决策的逻辑测试。

### Task 1: 建立服务端安全边界与 C 盘文件选择测试

**Files:**
- Modify: `tests/WeaponListSearch.Tests.ps1`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: 后续创建的 `AMacQWebEditorServer.ps1` 和 `启动AMacQ网页配置界面.vbs`。
- Produces: Pester 断言，锁定回环绑定、两次 C 盘初始目录、原子写入、静态资源白名单、15 分钟超时和启动器入口。

- [ ] **Step 1: 在 Pester 文件末尾新增失败断言**

```powershell
Describe 'Local browser service' {
    It 'uses only loopback, starts file dialogs from C drive, and keeps file access session-bound' {
        $root = Join-Path $PSScriptRoot '..'
        $serverPath = Join-Path $root 'AMacQWebEditorServer.ps1'
        $launcherPath = Join-Path $root '启动AMacQ网页配置界面.vbs'

        Test-Path $serverPath | Should Be $true
        Test-Path $launcherPath | Should Be $true

        $server = Get-Content -Raw $serverPath
        $launcher = Get-Content -Raw $launcherPath

        $server | Should Match 'http://127\.0\.0\.1:'
        $server | Should Not Match 'http://0\.0\.0\.0:|http://localhost:'
        ([regex]::Matches($server, "InitialDirectory\s*=\s*'C:\\'")).Count | Should Be 2
        $server | Should Match 'Move-Item -Force \$tempPath \$Path'
        $server | Should Match "'index\.html', 'styles\.css', 'app\.js'"
        $server | Should Match 'FromMinutes\(15\)'
        $server | Should Match '\$script:SelectedPaths'
        $server | Should Not Match 'Path\s*=\s*\$body\.'
        $launcher | Should Match 'AMacQWebEditorServer\.ps1'
        $launcher | Should Match '127\.0\.0\.1'
    }
}
```

- [ ] **Step 2: 运行 Pester，确认测试因服务文件不存在而失败**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"`

Expected: `Local browser service` 失败，提示 `AMacQWebEditorServer.ps1` 不存在；此前测试仍通过。

### Task 2: 实现回环服务、系统 C 盘文件选择与原子写回

**Files:**
- Create: `AMacQWebEditorServer.ps1`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: 参数 `-PortFile <path>`；`web/index.html`、`web/styles.css`、`web/app.js`。
- Produces: `GET /api/status`、`POST /api/select-files`、`POST /api/apply`、`POST /api/shutdown`，以及静态 `/`、`/styles.css`、`/app.js`。

- [ ] **Step 1: 创建 `AMacQWebEditorServer.ps1`，包含以下实现骨架和函数**

```powershell
param([Parameter(Mandatory=$true)][string]$PortFile)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$script:SelectedPaths = @{}
$script:SelectedEncodings = @{}
$script:LastRequestAt = [DateTime]::UtcNow
$script:ShutdownRequested = $false
$script:WebRoot = Join-Path $PSScriptRoot 'web'
$script:StaticFiles = @('index.html', 'styles.css', 'app.js')

function Get-FileEncoding {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { return [System.Text.UTF8Encoding]::new($true) }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) { return [System.Text.UnicodeEncoding]::new($false, $true) }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) { return [System.Text.BigEndianUnicodeEncoding]::new($true) }
    [System.Text.UTF8Encoding]::new($false)
}

function Read-LuaFile {
    param([string]$Path)
    $encoding = Get-FileEncoding $Path
    [pscustomobject]@{ Name=[System.IO.Path]::GetFileName($Path); Content=[System.IO.File]::ReadAllText($Path, $encoding); Encoding=$encoding }
}

function Save-LuaFile {
    param([string]$Path, [string]$Content, [System.Text.Encoding]$Encoding)
    $tempPath = "$Path.writing.$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        [System.IO.File]::WriteAllText($tempPath, $Content, $Encoding)
        Move-Item -Force $tempPath $Path
    } catch {
        if (Test-Path -LiteralPath $tempPath) { Remove-Item -Force $tempPath -ErrorAction SilentlyContinue }
        throw
    }
}

function Send-Json {
    param($Response, [int]$StatusCode, $Value)
    $data = [System.Text.Encoding]::UTF8.GetBytes(($Value | ConvertTo-Json -Compress -Depth 5))
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $data.Length
    $Response.OutputStream.Write($data, 0, $data.Length)
    $Response.Close()
}

function Select-ConfigFiles {
    $keyDialog = New-Object System.Windows.Forms.OpenFileDialog
    $keyDialog.Title = '选择第一个配置文件（按键配置）'
    $keyDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
    $keyDialog.InitialDirectory = 'C:\'
    if ($keyDialog.ShowDialog() -ne 'OK') { return $null }

    $sensitivityDialog = New-Object System.Windows.Forms.OpenFileDialog
    $sensitivityDialog.Title = '选择第二个配置文件（灵敏度配置）'
    $sensitivityDialog.Filter = 'Lua 文件 (*.lua)|*.lua|所有文件 (*.*)|*.*'
    $sensitivityDialog.InitialDirectory = 'C:\'
    if ($sensitivityDialog.ShowDialog() -ne 'OK') { return $null }
    if ($keyDialog.FileName -eq $sensitivityDialog.FileName) { throw '请为两个配置角色选择不同的文件。' }

    $keyFile = Read-LuaFile $keyDialog.FileName
    $sensitivityFile = Read-LuaFile $sensitivityDialog.FileName
    $script:SelectedPaths = @{ KeyBindings=$keyDialog.FileName; Sensitivity=$sensitivityDialog.FileName }
    $script:SelectedEncodings = @{ KeyBindings=$keyFile.Encoding; Sensitivity=$sensitivityFile.Encoding }
    @{ keyBindings=@{ name=$keyFile.Name; content=$keyFile.Content }; sensitivity=@{ name=$sensitivityFile.Name; content=$sensitivityFile.Content } }
}
```

- [ ] **Step 2: 在同一文件实现固定路由、服务循环和端口文件写入**

```powershell
function Get-RequestBody {
    param($Request)
    $reader = New-Object System.IO.StreamReader($Request.InputStream, $Request.ContentEncoding)
    try { $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
}

$listener = [System.Net.HttpListener]::new()
$port = 0
$tcp = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$tcp.Start(); $port = ([System.Net.IPEndPoint]$tcp.LocalEndpoint).Port; $tcp.Stop()
$listener.Prefixes.Add("http://127.0.0.1:$port/")
$listener.Start()
[System.IO.File]::WriteAllText($PortFile, $port.ToString(), [System.Text.Encoding]::ASCII)

try {
    while (!$script:ShutdownRequested) {
        if (([DateTime]::UtcNow - $script:LastRequestAt) -gt [TimeSpan]::FromMinutes(15)) { break }
        $pending = $listener.BeginGetContext($null, $null)
        if (!$pending.AsyncWaitHandle.WaitOne(1000)) { continue }
        $context = $listener.EndGetContext($pending)
        $script:LastRequestAt = [DateTime]::UtcNow
        $request = $context.Request; $path = $request.Url.AbsolutePath
        try {
            if ($path -eq '/api/status' -and $request.HttpMethod -eq 'GET') {
                Send-Json $context.Response 200 @{ available=$true; hasSelectedFiles=($script:SelectedPaths.Count -eq 2); idleSecondsRemaining=[Math]::Max(0, [int](900 - (([DateTime]::UtcNow - $script:LastRequestAt).TotalSeconds))) }
            } elseif ($path -eq '/api/select-files' -and $request.HttpMethod -eq 'POST') {
                $selection = Select-ConfigFiles
                if ($null -eq $selection) { Send-Json $context.Response 200 @{ cancelled=$true } } else { Send-Json $context.Response 200 @{ cancelled=$false; files=$selection } }
            } elseif ($path -eq '/api/apply' -and $request.HttpMethod -eq 'POST') {
                if ($script:SelectedPaths.Count -ne 2) { throw '请先通过服务选择两个配置文件。' }
                $body = Get-RequestBody $request
                if ($null -eq $body.keyBindingsContent -or $null -eq $body.sensitivityContent) { throw '保存请求缺少配置内容。' }
                Save-LuaFile $script:SelectedPaths.KeyBindings ([string]$body.keyBindingsContent) $script:SelectedEncodings.KeyBindings
                Save-LuaFile $script:SelectedPaths.Sensitivity ([string]$body.sensitivityContent) $script:SelectedEncodings.Sensitivity
                Send-Json $context.Response 200 @{ saved=$true }
            } elseif ($path -eq '/api/shutdown' -and $request.HttpMethod -eq 'POST') {
                Send-Json $context.Response 200 @{ stopping=$true }
                $script:ShutdownRequested = $true
            } else {
                $file = if ($path -eq '/') { 'index.html' } elseif ($path -eq '/styles.css') { 'styles.css' } elseif ($path -eq '/app.js') { 'app.js' } else { $null }
                if ($null -eq $file -or $script:StaticFiles -notcontains $file) { $context.Response.StatusCode = 404; $context.Response.Close(); continue }
                $contentType = if ($file -eq 'index.html') { 'text/html; charset=utf-8' } elseif ($file -eq 'styles.css') { 'text/css; charset=utf-8' } else { 'application/javascript; charset=utf-8' }
                $data = [System.IO.File]::ReadAllBytes((Join-Path $script:WebRoot $file))
                $context.Response.ContentType = $contentType; $context.Response.ContentLength64 = $data.Length
                $context.Response.OutputStream.Write($data, 0, $data.Length); $context.Response.Close()
            }
        } catch { Send-Json $context.Response 400 @{ error=$_.Exception.Message } }
    }
} finally {
    if ($listener.IsListening) { $listener.Stop() }; $listener.Close()
    if (Test-Path -LiteralPath $PortFile) { Remove-Item -Force $PortFile }
}
```

- [ ] **Step 3: 运行 Pester，确认服务安全断言通过**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"`

Expected: 所有现有断言和 `Local browser service` 均通过。

### Task 3: 添加网页服务启动器与网页服务模式 UI

**Files:**
- Create: `启动AMacQ网页配置界面.vbs`
- Modify: `web/index.html`
- Modify: `web/styles.css`
- Test: `tests/WeaponListSearch.Tests.ps1`

**Interfaces:**
- Consumes: `AMacQWebEditorServer.ps1 -PortFile <temporary-file>`；网页元素 id `service-status`、`shutdown-service`。
- Produces: 双击入口启动本机服务并打开 `http://127.0.0.1:<port>/`；网页显示服务连接状态和停止操作。

- [ ] **Step 1: 创建 `启动AMacQ网页配置界面.vbs`**

```vbscript
Option Explicit

Dim shell, fso, basePath, portFile, command, deadline, port, url
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
basePath = fso.GetParentFolderName(WScript.ScriptFullName)
portFile = fso.BuildPath(fso.GetSpecialFolder(2), "AMacQWebEditor-" & Replace(CStr(Timer), ".", "") & ".port")
command = "powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File """ & fso.BuildPath(basePath, "AMacQWebEditorServer.ps1") & """ -PortFile """ & portFile & """"
shell.Run command, 1, False

deadline = DateAdd("s", 10, Now)
Do While Now < deadline
    If fso.FileExists(portFile) Then
        Dim stream
        Set stream = fso.OpenTextFile(portFile, 1)
        port = Trim(stream.ReadAll)
        stream.Close
        If Len(port) > 0 Then Exit Do
    End If
    WScript.Sleep 100
Loop

If Len(port) = 0 Then
    MsgBox "网页配置服务启动失败。请确认 PowerShell 可用且端口未被安全软件拦截。", vbCritical, "AMacQ"
    WScript.Quit 1
End If

url = "http://127.0.0.1:" & port & "/"
shell.Run url, 1, False
```

- [ ] **Step 2: 在 `web/index.html` 的 `<footer class="action-bar">` 内、应用按钮前插入服务状态与退出按钮**

```html
<p id="service-status" role="status">正在检测本机服务…</p>
<button id="shutdown-service" type="button" hidden>退出网页服务</button>
```

- [ ] **Step 3: 在 `web/styles.css` 追加服务状态样式**

```css
#service-status { margin: 0; color: #9ee8ff; font-size: 12px; }
#shutdown-service { border-color: rgba(255, 188, 205, .28); color: #ffdbe4; }
#shutdown-service:hover:not(:disabled) { background: rgba(181, 66, 104, .34); }
```

- [ ] **Step 4: 运行 Pester，确认启动器断言通过**

Run: `powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"`

Expected: 所有 7 个 Pester 描述块通过。

### Task 4: 连接网页 API、纯网页回退与服务保存

**Files:**
- Modify: `web/app.js`
- Modify: `tests/browser-editor.test.js`
- Test: `tests/browser-editor.test.js`

**Interfaces:**
- Consumes: 服务接口 `/api/status`、`/api/select-files`、`/api/apply`、`/api/shutdown`；现有 `buildConfigModel(files)` 和 `applyConfiguration(model, selection)`。
- Produces: `isLocalServiceUrl(url: string): boolean`、`serviceResponseToFiles(response): {KeyBindings:{name,content,encoding},Sensitivity:{name,content,encoding}}`；服务模式下选择与保存改用 API，服务不可用时原有浏览器选择/下载流程继续可用。

- [ ] **Step 1: 在 `tests/browser-editor.test.js` 添加失败测试和导入**

在导入列表加入 `isLocalServiceUrl`、`serviceResponseToFiles`，并添加：

```js
test('accepts only loopback service URLs and maps selected service files', () => {
  assert.equal(isLocalServiceUrl('http://127.0.0.1:53120/'), true);
  assert.equal(isLocalServiceUrl('http://localhost:53120/'), false);
  assert.equal(isLocalServiceUrl('http://192.168.1.5:53120/'), false);
  assert.deepEqual(serviceResponseToFiles({
    keyBindings: { name: 'KeyBindings.lua', content: 'press = 1' },
    sensitivity: { name: 'Sensitivity.lua', content: 'AK_qq1156777787_X = 1' },
  }), {
    KeyBindings: { name: 'KeyBindings.lua', content: 'press = 1', encoding: 'utf-8' },
    Sensitivity: { name: 'Sensitivity.lua', content: 'AK_qq1156777787_X = 1', encoding: 'utf-8' },
  });
});
```

- [ ] **Step 2: 运行 Node 测试确认失败**

Run: `node --test tests/browser-editor.test.js`

Expected: FAIL，提示 `isLocalServiceUrl is not a function`。

- [ ] **Step 3: 在 `web/app.js` 添加 API 纯函数，并加入导出对象**

```js
function isLocalServiceUrl(url) {
  try { return new URL(url).hostname === '127.0.0.1'; } catch { return false; }
}

function serviceResponseToFiles(response) {
  return {
    KeyBindings: { name: response.keyBindings.name, content: response.keyBindings.content, encoding: 'utf-8' },
    Sensitivity: { name: response.sensitivity.name, content: response.sensitivity.content, encoding: 'utf-8' },
  };
}
```

- [ ] **Step 4: 在 `initializeBrowserEditor()` 中完成服务状态、选择、保存与停止接线**

```js
const serviceStatus = document.getElementById('service-status');
const shutdownService = document.getElementById('shutdown-service');
const serviceMode = isLocalServiceUrl(window.location.href);
const requestService = async (path, options = {}) => {
  const response = await fetch(path, options);
  const result = await response.json();
  if (!response.ok) throw new Error(result.error || '本机服务请求失败。');
  return result;
};

const refreshServiceStatus = async () => {
  if (!serviceMode) {
    serviceStatus.textContent = '纯网页模式：保存时下载同名文件。';
    return;
  }
  try {
    await requestService('/api/status');
    serviceStatus.textContent = '本机服务已连接，可直接保存。';
    shutdownService.hidden = false;
  } catch {
    serviceStatus.textContent = '本机服务不可用：将使用下载保存。';
    shutdownService.hidden = true;
  }
};
```

将选择文件点击处理器的开头替换为：

```js
if (serviceMode) {
  try {
    const result = await requestService('/api/select-files', { method: 'POST' });
    if (result.cancelled) { setStatus('已取消选择文件。'); return; }
    const files = serviceResponseToFiles(result.files);
    state.model = buildConfigModel(files);
    state.handles = { service: true };
    state.selectedWeapon = state.model.weapons[0];
    const keyContent = state.model.files.KeyBindings.content;
    populateSelect(elements.press, PRESS_OPTIONS, getLuaAssignments(keyContent).find(({ name }) => name === 'press').value);
    populateSelect(elements['mode-switch'], MODE_SWITCH_OPTIONS, getLuaStringValue(keyContent, 'modeswitch'));
    elements['selected-weapon'].textContent = `枪械：${state.selectedWeapon}`;
    renderWeapons(); renderFields(); elements.apply.disabled = false; elements['refresh-files'].disabled = false;
    setStatus('配置已从本机服务加载。');
  } catch (error) { setStatus(`选择文件失败：${error.message}`, true); }
  return;
}
```

将应用监听器中 `const mode = await saveModel(...)` 的保存部分替换为：

```js
let mode;
if (state.handles.service) {
  await requestService('/api/apply', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      keyBindingsContent: state.model.files.KeyBindings.content,
      sensitivityContent: state.model.files.Sensitivity.content,
    }),
  });
  mode = 'direct';
} else {
  mode = await saveModel(state.model, state.handles);
}
```

最后在初始化末尾添加：

```js
shutdownService.addEventListener('click', async () => {
  try { await requestService('/api/shutdown', { method: 'POST' }); window.close(); }
  catch (error) { setStatus(`停止服务失败：${error.message}`, true); }
});
refreshServiceStatus();
```

- [ ] **Step 5: 运行 Node 与 Pester 测试**

Run: `node --test tests/browser-editor.test.js; powershell -NoProfile -Command "Invoke-Pester -Path .\tests\WeaponListSearch.Tests.ps1"; node --check web/app.js`

Expected: Node 显示 8 个通过、0 个失败；Pester 所有测试通过；脚本语法检查无输出且退出码为 0。

### Task 5: 手工服务验收与安全检查

**Files:**
- Modify: 无。
- Test: `AMacQWebEditorServer.ps1`
- Test: `启动AMacQ网页配置界面.vbs`

**Interfaces:**
- Consumes: Tasks 1–4 的完整实现。
- Produces: 经手工验证的本机服务浏览器模式；纯网页与 WPF 模式继续存在。

- [ ] **Step 1: 启动服务模式**

Run: `start "" "启动AMacQ网页配置界面.vbs"`

Expected: 默认浏览器打开 `http://127.0.0.1:<随机端口>/`；状态显示“本机服务已连接，可直接保存”。

- [ ] **Step 2: 验证两次文件对话框都从 C 盘打开**

点击“选择文件…”。

Expected: 第一个按键配置选择框和第二个灵敏度配置选择框均初始显示 `C:\`；取消任何一个对话框不会清除当前加载配置。

- [ ] **Step 3: 验证直接保存和服务停止**

加载两个测试 Lua 文件，修改一个字段并点击“应用”，随后点击“退出网页服务”。

Expected: 修改直接写回源文件，页面显示已写回提示；退出后本机地址停止响应。

- [ ] **Step 4: 验证纯网页和 WPF 入口仍存在**

Run: `start "" "web\index.html"`

Expected: `file://` 页面仍可打开，服务不可用时明确显示下载模式；现有 `启动AMacQ配置界面.vbs` 仍可启动 WPF 编辑器。

- [ ] **Step 5: 检查服务脚本没有对外监听或任意静态文件映射**

Run: `rg -n "127\.0\.0\.1|0\.0\.0\.0|localhost|StaticFiles|Join-Path \$script:WebRoot" AMacQWebEditorServer.ps1`

Expected: 仅出现 `127.0.0.1` 回环前缀、静态文件白名单及白名单资源的 `Join-Path`；没有 `0.0.0.0` 或 `localhost` 监听前缀。

## Self-Review

- **规格覆盖：** Task 1 固定服务安全与 C 盘选择要求；Task 2 实现服务、编码保留、会话受限路径、原子写入、静态白名单和生命周期；Task 3 实现启动器和页面状态；Task 4 接入 API 与纯网页回退；Task 5 完成服务模式、C 盘初始目录、写回、退出、纯网页/WPF 保留和监听安全检查。
- **占位检查：** 每个任务包含文件路径、可执行测试、明确的实现代码或插入位置；不存在 TBD 或未定义的后续步骤。
- **一致性检查：** 网页端接口路径、服务端路径、返回字段和状态语义在 Tasks 2–4 中一致；服务只从 `$script:SelectedPaths` 保存，客户端只传递内容。
