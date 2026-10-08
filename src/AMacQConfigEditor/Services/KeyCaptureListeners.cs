using System;
using System.Runtime.InteropServices;

namespace AMacQConfigEditor.Services;

/// <summary>
/// 监听鼠标侧键，用于"选完枪械后直接按一下侧键完成绑定"。
///
/// 低级鼠标钩子只在监听期间安装，回调会尽快返回，避免系统因为超时把钩子摘掉。
/// 鼠标侧键在系统层面只会区分 XBUTTON1 / XBUTTON2，因此这里能稳定识别的是配置里的
/// 4（后退）与 5（前进）；6/7/8/9 这些由 G HUB 侧识别的按键无法从系统钩子获得，
/// 需要在窗口里用数字键指定。
/// </summary>
internal sealed class MouseSideButtonListener : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_XBUTTONDOWN = 0x020B;
    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    private readonly Action<string> _onCaptured;
    private readonly HookProc _callback;
    private IntPtr _hook;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseLowLevelHookStruct
    {
        public int PointX;
        public int PointY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public MouseSideButtonListener(Action<string> onCaptured)
    {
        _onCaptured = onCaptured ?? throw new ArgumentNullException(nameof(onCaptured));
        _callback = HandleHook;
    }

    /// <summary>安装钩子；安装失败时返回 false，调用方应回退到手动选择键值。</summary>
    public bool TryStart()
    {
        if (_hook != IntPtr.Zero) return true;

        _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    /// <summary>把钩子回调里的原始鼠标数据换算成配置中的按键值；无法识别时返回 null。</summary>
    internal static string? TranslateXButton(uint xButton) => xButton switch
    {
        XButton1 => "4",
        XButton2 => "5",
        _ => null
    };

    private IntPtr HandleHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == WM_XBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<MouseLowLevelHookStruct>(lParam);
            var xButton = (info.MouseData >> 16) & 0xFFFF;
            var value = TranslateXButton(xButton);
            if (value is not null)
            {
                _onCaptured(value);
                return (IntPtr)1; // 绑定期间吞掉这次点击，避免同时触发游戏内动作
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}

/// <summary>
/// 监听数字键 4/5/6/7/8/9/0，作为侧键监听的补充：某些鼠标（或 G HUB 的按键映射）
/// 无法从系统钩子拿到侧键，此时用户在监听期间直接敲数字键也能完成绑定。
/// </summary>
internal sealed class DigitKeyListener : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private readonly Action<string> _onCaptured;
    private readonly HookProc _callback;
    private IntPtr _hook;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardLowLevelHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public DigitKeyListener(Action<string> onCaptured)
    {
        _onCaptured = onCaptured ?? throw new ArgumentNullException(nameof(onCaptured));
        _callback = HandleHook;
    }

    public bool TryStart()
    {
        if (_hook != IntPtr.Zero) return true;

        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    /// <summary>只接受可用于按键绑定的数字键，其它按键一律放行。</summary>
    internal static string? TranslateVirtualKey(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39) return ((char)virtualKey).ToString();        // 主键盘 0-9
        if (virtualKey is >= 0x60 and <= 0x69) return (virtualKey - 0x60).ToString();        // 小键盘 0-9
        return null;
    }

    private IntPtr HandleHook(int code, IntPtr wParam, IntPtr lParam)
    {
        var message = wParam.ToInt32();
        if (code >= 0 && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
        {
            var info = Marshal.PtrToStructure<KeyboardLowLevelHookStruct>(lParam);
            var value = TranslateVirtualKey(info.VirtualKeyCode);
            if (value is not null)
            {
                _onCaptured(value);
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
