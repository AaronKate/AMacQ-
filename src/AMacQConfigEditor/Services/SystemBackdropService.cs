using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AMacQConfigEditor.Services;

internal static class SystemBackdropService
{
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWA_MICA_EFFECT = 1029;
    private const int DWMSBT_MAINWINDOW = 2;
    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) => ApplyToWindow(new WindowInteropHelper(window).Handle);
    }

    private static void ApplyToWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;

        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && TryEnableMica(windowHandle)) return;
            EnableAcrylic(windowHandle);
        }
        catch
        {
            // Keep the existing WPF theme when the system backdrop is unavailable.
        }
    }

    private static bool TryEnableMica(IntPtr windowHandle)
    {
        var backdropType = DWMSBT_MAINWINDOW;
        if (DwmSetWindowAttribute(windowHandle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int)) == 0) return true;

        var micaEnabled = 1;
        return DwmSetWindowAttribute(windowHandle, DWMWA_MICA_EFFECT, ref micaEnabled, sizeof(int)) == 0;
    }

    private static void EnableAcrylic(IntPtr windowHandle)
    {
        var accentPolicy = new AccentPolicy
        {
            AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = unchecked((int)0xB5341F12)
        };

        var accentPolicySize = Marshal.SizeOf<AccentPolicy>();
        var accentPolicyPointer = Marshal.AllocHGlobal(accentPolicySize);
        try
        {
            Marshal.StructureToPtr(accentPolicy, accentPolicyPointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = accentPolicyPointer,
                SizeOfData = accentPolicySize
            };
            _ = SetWindowCompositionAttribute(windowHandle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPolicyPointer);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }
}
