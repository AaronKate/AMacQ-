# 深色原生标题栏 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 AMacQ WPF 编辑器使用 Windows 原生深色标题栏，同时保留系统窗口控制按钮和所有窗口交互行为。

**Architecture:** 在 `AMacQGuiEditor.ps1` 新增一个负责启用 DWM 沉浸式深色模式的函数。该函数通过窗口 HWND 调用 `DwmSetWindowAttribute`，优先使用 Windows 新版本属性 `20`，失败后兼容尝试属性 `19`；所有不支持或 API 调用失败情形均静默返回。窗口在 `SourceInitialized` 事件中调用函数，以确保原生 HWND 已创建。

**Tech Stack:** Windows PowerShell、WPF、Windows DWM API（`dwmapi.dll`）、P/Invoke。

## Global Constraints

- 仅修改 `AMacQGuiEditor.ps1`。
- 不修改窗口布局、`WindowStyle`、应用内画刷资源、动画或按钮样式。
- 不自定义标题栏，也不替换系统最小化、最大化和关闭按钮。
- 在不支持沉浸式深色标题栏的 Windows 环境静默回退到系统默认标题栏，且不能阻止应用启动。

---

### Task 1: 增加深色标题栏启用函数并接入窗口启动流程

**Files:**
- Modify: `AMacQGuiEditor.ps1:212-251`
- Modify: `AMacQGuiEditor.ps1:758-964`

**Interfaces:**
- Consumes: `System.Windows.Window` 实例，使用 `System.Windows.Interop.WindowInteropHelper` 获取其 `Handle`。
- Produces: `Set-DarkTitleBar([Windows.Window]$Window)`；函数无返回值，在运行时安全请求 DWM 深色标题栏。

- [ ] **Step 1: 写入可执行的函数级验证脚本**

在 PowerShell 中执行以下命令，验证 `DwmSetWindowAttribute` 的 P/Invoke 定义可以编译，并能针对当前 PowerShell 进程窗口句柄接受调用结果而不抛出异常：

```powershell
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DwmTitleBarProbe {
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
'@
$value = 1
[DwmTitleBarProbe]::DwmSetWindowAttribute([IntPtr]::Zero, 20, [ref]$value, [Runtime.InteropServices.Marshal]::SizeOf([int]))
```

预期：类型可成功定义；调用返回一个整数 HRESULT（返回值可能表示无效窗口句柄），但命令不出现未找到类型或 PowerShell 异常。

- [ ] **Step 2: 在 `Start-AnimatedBackground` 前新增函数**

在 `Read-AMacQConfig` 结束后、`Start-AnimatedBackground` 前插入以下函数：

```powershell
function Set-DarkTitleBar {
    param([Windows.Window]$Window)

    if (!$Window) { return }
    try {
        if (!('AMacQ.NativeMethods' -as [type])) {
            Add-Type @'
using System;
using System.Runtime.InteropServices;
namespace AMacQ {
    public static class NativeMethods {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
'@
        }

        $handle = [Windows.Interop.WindowInteropHelper]::new($Window).Handle
        if ($handle -eq [IntPtr]::Zero) { return }

        $enabled = 1
        $size = [Runtime.InteropServices.Marshal]::SizeOf([int])
        foreach ($attribute in 20, 19) {
            if ([AMacQ.NativeMethods]::DwmSetWindowAttribute($handle, $attribute, [ref]$enabled, $size) -eq 0) {
                return
            }
        }
    } catch {
        # Unsupported Windows versions retain the system title bar.
    }
}
```

- [ ] **Step 3: 在窗口显示前调用函数**

在 `Start-Gui` 中，找到：

```powershell
$window = [Windows.Markup.XamlReader]::Parse($xaml)
```

将其替换为：

```powershell
$window = [Windows.Markup.XamlReader]::Parse($xaml)
$window.Add_SourceInitialized({
    Set-DarkTitleBar $this
})
```

函数调用必须保持在 `ShowDialog()` 前；不得更改 XAML 中的窗口样式或布局。

- [ ] **Step 4: 解析 PowerShell 脚本**

运行：

```bash
powershell.exe -NoProfile -Command '$tokens = $null; $errors = $null; [System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path ".\AMacQGuiEditor.ps1"), [ref]$tokens, [ref]$errors) | Out-Null; if ($errors.Count) { $errors | ForEach-Object { $_.ToString() }; exit 1 }'
```

预期：退出码 `0`，无输出。

- [ ] **Step 5: 启动窗口进行手动验证**

运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\AMacQGuiEditor.ps1
```

预期：支持该 DWM 属性的 Windows 环境显示深色原生标题栏；最小化、最大化、关闭、拖动、双击最大化和边缘缩放均保持可用。关闭窗口后命令退出；不支持时窗口仍应正常显示系统默认标题栏。

- [ ] **Step 6: 检查变更范围并提交**

运行：

```bash
git diff --check
git diff -- AMacQGuiEditor.ps1
git status --short
git add AMacQGuiEditor.ps1
git commit -m "Add dark native title bar"
```

预期：仅 `AMacQGuiEditor.ps1` 进入此功能提交，且提交包含 DWM 主题函数与窗口显示前的调用。

## Self-Review

- **Spec coverage:** 任务 1 仅修改主 PowerShell 脚本，实现属性 `20` 优先、`19` 回退、API 异常静默回退，并在显示窗口前调用；不改变窗口布局或原生按钮。
- **Placeholder scan:** 计划提供完整函数、精确插入位置和验证命令，无待补全项。
- **Type consistency:** `Set-DarkTitleBar` 接收 `Windows.Window`，使用 `WindowInteropHelper` 返回 `IntPtr`，并以 `int` 值及其大小调用 `AMacQ.NativeMethods.DwmSetWindowAttribute`。
