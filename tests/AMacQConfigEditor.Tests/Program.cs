using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AMacQConfigEditor;
using AMacQConfigEditor.Services;
using AMacQConfigEditor.ViewModels;
using Forms = System.Windows.Forms;
// WPF 与 WinForms 同名类型的显式别名（两个命名空间都被引用）
using Application = System.Windows.Application;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using TextBox = System.Windows.Controls.TextBox;

namespace AMacQConfigEditor.Tests;

/// <summary>
/// 常驻回归测试。没有引入测试框架依赖：直接跑一个可执行程序，
/// 逐项打印 PASS/FAIL，任意一项失败则以非 0 退出码结束。
///
/// 运行方式：
///   dotnet run --project tests\AMacQConfigEditor.Tests\AMacQConfigEditor.Tests.csproj
///
/// 覆盖的都是本项目历史上真实出过问题的地方（配置写回语义、按键槽位映射、
/// 搜索排序、非标准键值 6/7/8/9 导致保存失败等），改动这些逻辑时请先跑一遍。
/// </summary>
internal static class TestRunner
{
    private static int _passed;
    private static readonly List<string> Failures = [];

    [STAThread]
    private static int Main()
    {
        global::System.Windows.Forms.Application.EnableVisualStyles();
        System.Threading.SynchronizationContext.SetSynchronizationContext(new global::System.Windows.Forms.WindowsFormsSynchronizationContext());
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        try
        {
            LuaParsingTests.Run();
            LuaWriteTests.Run();
            WeaponFilterTests.Run();
            SlotBindingTests.Run();
            KeyListenerTests.Run();
            QuickSwitchWindowTests.Run();
            ConfigurationFileStateTests.Run();
            EncodingTests.Run();
            TrayMenuStructureTests.Run();
            SaveWithNonStandardBindingTests.Run();
        }
        catch (Exception exception)
        {
            Fail($"运行期异常：{exception.GetType().FullName}: {exception.Message}\n{exception.StackTrace}");
        }
        finally
        {
            app.Shutdown();
        }

        Console.WriteLine();
        Console.WriteLine($"通过 {_passed} 项，失败 {Failures.Count} 项");
        foreach (var failure in Failures) Console.WriteLine($"  - {failure}");
        return Failures.Count == 0 ? 0 : 1;
    }

    internal static void Pass(string label)
    {
        _passed++;
        Console.WriteLine($"PASS  {label}");
    }

    internal static void Fail(string label)
    {
        Failures.Add(label);
        Console.WriteLine($"FAIL  {label}");
    }

    internal static void Check(bool condition, string label)
    {
        if (condition) Pass(label);
        else Fail(label);
    }

    internal static void CheckEqual<T>(T expected, T actual, string label) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"{label}（期望 {Describe(expected)}，实际 {Describe(actual)}）");

    private static string Describe<T>(T value)
    {
        if (value is null) return "null";
        var text = value.ToString() ?? string.Empty;
        // 多行内容（例如整份配置）只显示摘要，避免刷屏
        var singleLine = text.Replace("\r", string.Empty).Replace('\n', '⏎');
        return singleLine.Length <= 60 ? $"“{singleLine}”" : $"“{singleLine.Substring(0, 57)}…”";
    }

    /// <summary>跑完消息泵，让 Dispatcher 上的排队工作（窗口关闭、BeginInvoke）落地。</summary>
    internal static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        global::System.Windows.Forms.Application.DoEvents();
    }
}

/// <summary>Lua 解析：枪械识别、取值。</summary>
internal static class LuaParsingTests
{
    private const string Content = """
        press = 1
        modeswitch = "scrolllock"
        AK12_qq1156777787 = 4
        AK12_Third = 5
        M14_qq1156777787_second = 9
        AKS-74_qq1156777787 = 0
        press_Third = 1
        """;

    public static void Run()
    {
        var weapons = LuaConfigService.GetPrimaryWeapons(Content);
        TestRunner.Check(weapons.Contains("AK12"), "识别普通枪械名");
        TestRunner.Check(weapons.Contains("M14"), "识别带 Alt 槽位的枪械");
        TestRunner.Check(weapons.Contains("AKS-74"), "识别带连字符的枪械名（AKS-74）");
        TestRunner.Check(!weapons.Contains("press"), "全局变量 press_Third 不会被当成枪械");
        TestRunner.CheckEqual(3, weapons.Count, "枪械数量");

        TestRunner.CheckEqual("4", LuaConfigService.GetNumber(Content, "AK12_qq1156777787"), "读取数字值");
        TestRunner.CheckEqual("scrolllock", LuaConfigService.GetString(Content, "modeswitch"), "读取带引号的字符串值");
        TestRunner.Check(LuaConfigService.GetNumber(Content, "不存在的变量") is null, "读取不存在的变量返回 null");
    }
}

/// <summary>Lua 写回：批量修改与逐条替换等价、冲突清理。</summary>
internal static class LuaWriteTests
{
    private const string Original = """
        press = 1
        modeswitch = "scrolllock"
        AK12_qq1156777787 = 4
        AK12_qq1156777787_second = 0
        M4A1_qq1156777787 = 0
        M4A1_qq1156777787_second = 0
        """;

    public static void Run()
    {
        // 批量写入的结果必须与逐条写入一致
        var document = LuaConfigService.CreateDocument(Original);
        document.SetNumber("M4A1_qq1156777787", "4");
        document.ClearConflictingBinding("M4A1", "qq1156777787", "4");
        document.SetNumber("press", "3");
        var batched = document.ApplyPendingChanges();

        var sequential = Original;
        sequential = ReplaceOnce(sequential, "M4A1_qq1156777787", "4");
        sequential = ReplaceOnce(sequential, "AK12_qq1156777787", "0"); // 同槽位冲突被清零
        sequential = ReplaceOnce(sequential, "press", "3");

        TestRunner.CheckEqual(sequential, batched, "批量写入与逐条写入结果一致");

        // 冲突清理只影响同槽位、同键值的其它枪械
        var conflict = LuaConfigService.CreateDocument(Original);
        conflict.ClearConflictingBinding("M4A1", "qq1156777787", "4");
        var result = conflict.ApplyPendingChanges();
        TestRunner.Check(result.Contains("AK12_qq1156777787 = 0"), "同槽位同键值的其它枪械被清零");
        TestRunner.Check(result.Contains("M4A1_qq1156777787 = 0"), "本枪械的值不被冲突清理改动");
        TestRunner.Check(result.Contains("AK12_qq1156777787_second = 0"), "不同槽位不受影响");

        // 写入 0 表示解绑，不触发冲突清理
        var unbound = LuaConfigService.CreateDocument(Original);
        unbound.ClearConflictingBinding("M4A1", "qq1156777787", "0");
        TestRunner.CheckEqual(Original, unbound.ApplyPendingChanges(), "键值为 0 时不做冲突清理");

        // 字符串写入只改带引号的赋值行（与旧实现保持一致的行为）
        var quoted = LuaConfigService.CreateDocument(Original);
        quoted.SetString("modeswitch", "capslock");
        TestRunner.Check(quoted.ApplyPendingChanges().Contains("modeswitch = \"capslock\""), "带引号的字符串值可被改写");

        var bare = LuaConfigService.CreateDocument("modeswitch = scrolllock\n");
        bare.SetString("modeswitch", "capslock");
        TestRunner.CheckEqual("modeswitch = scrolllock\n", bare.ApplyPendingChanges(), "裸值写法保持原样（已知行为）");
    }

    private static string ReplaceOnce(string content, string variableName, string value)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith(variableName, StringComparison.Ordinal)) continue;
            var rest = trimmed.Substring(variableName.Length).TrimStart();
            if (!rest.StartsWith("=", StringComparison.Ordinal)) continue;
            var indent = lines[i].Substring(0, lines[i].Length - trimmed.Length);
            var lineEnd = lines[i].EndsWith("\r", StringComparison.Ordinal) ? "\r" : string.Empty;
            lines[i] = $"{indent}{variableName} = {value}{lineEnd}";
            break;
        }
        return string.Join("\n", lines);
    }
}

/// <summary>枪械搜索过滤与排序。</summary>
internal static class WeaponFilterTests
{
    private sealed class Row(string name, string displayName, string summary)
    {
        public string Name { get; } = name;
        public string DisplayName { get; } = displayName;
        public string Summary { get; } = summary;
    }

    private static readonly List<Row> Rows =
    [
        new("MDR", "MDR", ""),
        new("TOM", "汤姆逊", "4 · Alt+5"),
        new("AKM", "AKM", "6"),
        new("M4A1", "M4A1", "5"),
        new("AK12", "AK-12", ""),
        new("MK4", "MK4", "9"),
        new("MK47", "MK47", "9"),
        new("YeNiu", "野牛", ""),
    ];

    public static void Run()
    {
        TestRunner.CheckEqual(Rows.Count, Filter("").Count, "空查询返回全部");
        TestRunner.CheckEqual(Rows.Count, Filter("   ").Count, "纯空格查询返回全部");

        var mk4 = Filter("mk4");
        TestRunner.CheckEqual("MK4", mk4[0].Name, "输入 mk4 时 MK4 排在最前");

        var mk = Filter("mk");
        TestRunner.Check(mk.Select(r => r.Name).SequenceEqual(["MK4", "MK47"]), "同为前缀时保持原始顺序");

        TestRunner.CheckEqual("AKM", Filter("akm")[0].Name, "代码完全匹配优先");
        TestRunner.CheckEqual("YeNiu", Filter("野牛").Single().Name, "中文显示名可命中");
        TestRunner.CheckEqual("TOM", Filter("汤姆").Single().Name, "中文显示名支持子串");
        TestRunner.Check(Filter("9").Any(r => r.Name == "MK4"), "按键摘要里的数字可命中");
        TestRunner.CheckEqual(0, Filter("zzzz").Count, "无匹配时结果为空");
        TestRunner.Check(Filter("MK").Count == Filter("mk").Count, "筛选不区分大小写");
        TestRunner.Check(Filter("mk 9")[0].Name == "MK4", "多关键词按“与”匹配");
    }

    private static List<Row> Filter(string query) =>
        WeaponFilter.Apply(Rows, query, row => row.Name, row => row.DisplayName, row => row.Summary);
}

/// <summary>按键槽位映射与冲突检测。</summary>
internal static class SlotBindingTests
{
    public static void Run()
    {
        TestRunner.CheckEqual("qq1156777787", MainWindowViewModel.SuffixFor(WeaponBindingSlot.Primary), "主键槽位后缀");
        TestRunner.CheckEqual("qq1156777787_second", MainWindowViewModel.SuffixFor(WeaponBindingSlot.Alt), "Alt 槽位后缀");
        TestRunner.CheckEqual("Third", MainWindowViewModel.SuffixFor(WeaponBindingSlot.Ctrl), "Ctrl 槽位后缀");

        var viewModel = TestConfig.Load("AK12_qq1156777787 = 4\nAK12_Third = 5\nM4A1_qq1156777787 = 0\n");
        TestRunner.CheckEqual("4", viewModel.GetSlotBindingValue("AK12", WeaponBindingSlot.Primary), "读取主键槽位");
        TestRunner.CheckEqual("5", viewModel.GetSlotBindingValue("AK12", WeaponBindingSlot.Ctrl), "读取 Ctrl 槽位");
        TestRunner.CheckEqual("0", viewModel.GetSlotBindingValue("M4A1", WeaponBindingSlot.Primary), "未绑定时读取为 0");

        TestRunner.CheckEqual("AK12", viewModel.FindBindingConflict("M4A1", WeaponBindingSlot.Primary, "4"), "检测同槽位冲突");
        TestRunner.Check(viewModel.FindBindingConflict("M4A1", WeaponBindingSlot.Alt, "4") is null, "不同槽位互不冲突");
        TestRunner.Check(viewModel.FindBindingConflict("M4A1", WeaponBindingSlot.Primary, "0") is null, "写入 0 不产生冲突");
        TestRunner.Check(viewModel.FindBindingConflict("M4A1", WeaponBindingSlot.Primary, "9") is null, "空闲键值不产生冲突");

        TestConfig.Cleanup();
    }
}

/// <summary>键值换算：侧键与数字键。</summary>
internal static class KeyListenerTests
{
    public static void Run()
    {
        TestRunner.CheckEqual("4", MouseSideButtonListener.TranslateXButton(1), "XBUTTON1 译为 4（后退）");
        TestRunner.CheckEqual("5", MouseSideButtonListener.TranslateXButton(2), "XBUTTON2 译为 5（前进）");
        TestRunner.Check(MouseSideButtonListener.TranslateXButton(3) is null, "未知 XBUTTON 不产生键值");

        TestRunner.CheckEqual("0", DigitKeyListener.TranslateVirtualKey(0x30), "主键盘 0");
        TestRunner.CheckEqual("9", DigitKeyListener.TranslateVirtualKey(0x39), "主键盘 9");
        TestRunner.CheckEqual("4", DigitKeyListener.TranslateVirtualKey(0x64), "小键盘 4");
        TestRunner.Check(DigitKeyListener.TranslateVirtualKey(0x41) is null, "字母键不产生键值");
    }
}

/// <summary>快速切换窗口：筛选、选中、确认、取消。</summary>
internal static class QuickSwitchWindowTests
{
    private sealed class FakeHost : IWeaponQuickSwitchHost
    {
        public string? ConfirmedWeapon { get; private set; }
        public int ConfirmCount { get; private set; }

        public IReadOnlyList<string> WeaponNames { get; } = ["AK12", "M4A1", "MK4", "MK47", "YeNiu"];

        public string DisplayNameFor(string weapon) => weapon == "YeNiu" ? "野牛" : weapon;

        public string BindingSummaryFor(string weapon) => weapon == "MK4" ? "9" : string.Empty;

        public string? CurrentWeaponName => "M4A1";

        public void ConfirmWeapon(string weapon, WeaponBindingSlot slot)
        {
            ConfirmCount++;
            ConfirmedWeapon = weapon;
        }

        public string BindingValueFor(string weapon, WeaponBindingSlot slot) => "0";

        public string? ApplyBinding(string weapon, WeaponBindingSlot slot, string value) => null;
    }

    public static void Run()
    {
        var host = new FakeHost();
        var window = new QuickSwitchWindow(host);
        window.Show();
        window.UpdateLayout();
        TestRunner.Pump();

        TestRunner.Check(window.IsVisible, "窗口可以构造并显示");
        var list = (ListBox)window.FindName("WeaponList");
        TestRunner.CheckEqual(host.WeaponNames.Count, list.Items.Count, "未输入时列出全部枪械");

        SetSearchText(window, "mk");
        TestRunner.Pump();
        TestRunner.CheckEqual(2, list.Items.Count, "输入 mk 命中 2 把");
        TestRunner.CheckEqual("MK4", SelectedName(window), "自动选中第一条命中");

        // 列表显示的是中文名（曾错误地绑定内部代码）
        SetSearchText(window, "野牛");
        TestRunner.Pump();
        TestRunner.CheckEqual("野牛", FirstRowText(window), "列表显示中文显示名");

        SetSearchText(window, "mk4");
        TestRunner.Pump();
        TestRunner.Check(PressSearchKey(window, Key.Enter), "Enter 被识别为已处理");
        TestRunner.CheckEqual("MK4", host.ConfirmedWeapon, "Enter 确认选中的枪械");
        TestRunner.Check(!window.IsVisible, "确认后窗口关闭");

        var cancelHost = new FakeHost();
        var cancelWindow = new QuickSwitchWindow(cancelHost);
        cancelWindow.Show();
        cancelWindow.UpdateLayout();
        TestRunner.Pump();
        SetSearchText(cancelWindow, "mk4");
        TestRunner.Pump();
        TestRunner.Check(PressSearchKey(cancelWindow, Key.Escape), "Esc 被识别为已处理");
        TestRunner.CheckEqual(0, cancelHost.ConfirmCount, "Esc 不触发切换");
        TestRunner.Check(!cancelWindow.IsVisible, "Esc 关闭窗口");
    }

    private static void SetSearchText(QuickSwitchWindow window, string text) =>
        ((TextBox)window.FindName("SearchBox")).Text = text;

    private static string SelectedName(QuickSwitchWindow window)
    {
        var item = ((ListBox)window.FindName("WeaponList")).SelectedItem;
        return item?.GetType().GetProperty("Name")?.GetValue(item) as string ?? string.Empty;
    }

    private static string FirstRowText(QuickSwitchWindow window)
    {
        var list = (ListBox)window.FindName("WeaponList");
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromIndex(0) is not ListBoxItem container) return "(未生成行)";
        var textBlock = FindDescendant<TextBlock>(container, "RowName");
        return textBlock?.Text ?? "(未找到文本)";
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name) return element;
            var nested = FindDescendant<T>(child, name);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static bool PressSearchKey(QuickSwitchWindow window, Key key)
    {
        var method = typeof(QuickSwitchWindow).GetMethod("HandleSearchKey", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 HandleSearchKey");
        return (bool)method.Invoke(window, [key, ModifierKeys.None])!;
    }
}

/// <summary>
/// 托盘菜单结构：枪械列表必须是"骨架 + 按需展开"的懒加载结构。
/// 全量预建会产生 500+ 个菜单项、让菜单出现要等约 250ms，因此这里把结构固定下来。
/// </summary>
internal static class TrayMenuStructureTests
{
    public static void Run()
    {
        // 隔离真实部署目录：MainWindow 的构造与关闭会改名部署目录里的配置文件
        using var guard = InstallRecordGuard.Hide();
        var config = TestConfig.Create("AK12_qq1156777787 = 4\nAK12_Third = 5\nM14_qq1156777787_second = 9\n");
        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();
        TestRunner.Pump();
        TestConfig.PointMainWindowAt(window, config);
        Invoke(window, "LoadFiles");
        TestRunner.Pump();
        Invoke(window, "RefreshTrayWeaponMenu");
        TestRunner.Pump();

        var topLevel = Field<System.Collections.IList>(window, "_trayWeaponMenuItems")!;
        TestRunner.Check(topLevel.Count > 0, "托盘菜单已生成枪械分类");

        var weaponMenu = FindWeaponMenu(topLevel, "AK12");
        TestRunner.Check(weaponMenu is not null, "分类下能找到 AK12");
        if (weaponMenu is null)
        {
            window.Close();
            TestRunner.Pump();
            TestConfig.Cleanup();
            return;
        }

        // 懒加载：未展开时只有"仅选择此枪械"和分隔线
        TestRunner.CheckEqual(2, weaponMenu.DropDownItems.Count, "未展开时不预建按键位子菜单");

        InvokeWithArgs(window, "PopulateTrayBindingMenus", weaponMenu, "AK12");
        TestRunner.CheckEqual(5, weaponMenu.DropDownItems.Count, "展开后补齐三个按键位子菜单");

        var labels = weaponMenu.DropDownItems.Cast<Forms.ToolStripItem>().Select(item => item.Text).ToArray();
        TestRunner.Check(labels.Contains("无修饰键"), "包含主键槽位");
        TestRunner.Check(labels.Contains("按住 Alt"), "包含 Alt 槽位");
        TestRunner.Check(labels.Contains("按住 Ctrl"), "包含 Ctrl 槽位");

        var primaryMenu = (Forms.ToolStripMenuItem)weaponMenu.DropDownItems[2]!;
        var checkedOption = primaryMenu.DropDownItems.Cast<Forms.ToolStripMenuItem>().FirstOrDefault(item => item.Checked);
        TestRunner.CheckEqual("4", checkedOption?.Tag as string ?? "(无勾选)", "当前键值对应的选项被勾选");

        // 重复展开不应累积菜单项
        InvokeWithArgs(window, "PopulateTrayBindingMenus", weaponMenu, "AK12");
        TestRunner.CheckEqual(5, weaponMenu.DropDownItems.Count, "重复展开不会累积子菜单");

        // 勾选状态跟随当前枪械
        var selectWeapon = typeof(MainWindow).GetMethod("SelectWeaponFromTrayOnUiThread", BindingFlags.Instance | BindingFlags.NonPublic)!;
        selectWeapon.Invoke(window, ["M14"]);
        TestRunner.Pump();
        Invoke(window, "UpdateTrayWeaponCheckMarks");
        TestRunner.CheckEqual(true, FindWeaponMenu(topLevel, "M14")?.Checked, "当前枪械被标记勾选");
        TestRunner.CheckEqual(false, weaponMenu.Checked, "其它枪械不被勾选");

        window.Close();
        TestRunner.Pump();
        TestConfig.Cleanup();
    }

    private static Forms.ToolStripMenuItem? FindWeaponMenu(System.Collections.IList topLevel, string weapon)
    {
        foreach (var item in topLevel)
        {
            if (item is not Forms.ToolStripMenuItem category) continue;
            foreach (Forms.ToolStripItem child in category.DropDownItems)
            {
                if (child is Forms.ToolStripMenuItem weaponItem && (weaponItem.Tag as string) == weapon) return weaponItem;
            }
        }
        return null;
    }

    private static void Invoke(object target, string methodName) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

    private static void InvokeWithArgs(object target, string methodName, params object[] arguments) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);

    private static T? Field<T>(object target, string fieldName) where T : class =>
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target) as T;
}

/// <summary>
/// 回归：键值为 6/7/8/9 的枪械切换后必须能正常保存，且原值不能被清空。
/// 曾经的问题：下拉框选项按鼠标型号生成（通用鼠标只有 0/4/5），表示不了 9，
/// 于是把绑定源清成 null，点"应用"直接抛 ArgumentNullException。
/// </summary>
internal static class SaveWithNonStandardBindingTests
{
    public static void Run()
    {
        // 隔离真实部署目录：MainWindow 的构造与关闭会改名部署目录里的配置文件
        using var guard = InstallRecordGuard.Hide();
        var config = TestConfig.Create(
            "press = 1\nM14_qq1156777787 = 0\nM14_qq1156777787_second = 9\nM14_Third = 0\nAKM_Third = 6\n");

        var window = new MainWindow();
        window.Show();
        window.UpdateLayout();
        TestRunner.Pump();
        TestConfig.PointMainWindowAt(window, config);
        Invoke(window, "LoadFiles");
        TestRunner.Pump();

        var viewModel = Field<MainWindowViewModel>(window, "_viewModel")!;

        SelectWeapon(window, "M14");
        TestRunner.CheckEqual("9", viewModel.AltKey, "切换后 Alt 键值保持为 9（不被下拉框清空）");
        TestRunner.Check(SaveSucceeds(viewModel, out var error), $"键值 9 的枪械可以保存（{error}）");
        TestRunner.CheckEqual("9", ReadBinding(config.KeyPath, "M14_qq1156777787_second"), "保存后文件中的 Alt 键值仍为 9");

        SelectWeapon(window, "AKM");
        TestRunner.CheckEqual("6", viewModel.CtrlKey, "切换后 Ctrl 键值保持为 6");
        TestRunner.Check(SaveSucceeds(viewModel, out error), $"键值 6 的枪械可以保存（{error}）");
        TestRunner.CheckEqual("6", ReadBinding(config.KeyPath, "AKM_Third"), "保存后文件中的 Ctrl 键值仍为 6");

        // 切换鼠标型号也不能把值清空
        var modelList = (ComboBox)window.FindName("MouseModelList");
        foreach (var model in new[] { "gpw", "generic" })
        {
            modelList.SelectedValue = model;
            TestRunner.Pump();
            TestRunner.CheckEqual("6", viewModel.CtrlKey, $"切换到鼠标型号 {model} 后 Ctrl 键值仍为 6");
        }

        // 非法值必须在写盘前被拦住，并给出可读的提示
        viewModel.CtrlKey = null!;
        TestRunner.Check(!SaveSucceeds(viewModel, out error), "空键值被校验拦下，不会写进配置文件");
        TestRunner.Check(error.Contains("Ctrl") || error.Contains("按住 Ctrl"), $"校验提示指出具体字段（{error}）");

        window.Close();
        TestRunner.Pump();
        TestConfig.Cleanup();
    }

    private static bool SaveSucceeds(MainWindowViewModel viewModel, out string error)
    {
        try
        {
            viewModel.Save();
            error = "OK";
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    private static void SelectWeapon(MainWindow window, string weapon)
    {
        var method = typeof(MainWindow).GetMethod("SelectWeaponFromTrayOnUiThread", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 SelectWeaponFromTrayOnUiThread");
        method.Invoke(window, [weapon]);
        TestRunner.Pump();
    }

    private static void Invoke(object target, string methodName) =>
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

    private static T? Field<T>(object target, string fieldName) where T : class =>
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target) as T;

    private static string ReadBinding(string path, string variableName)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var index = line.IndexOf('=');
            if (index <= 0) continue;
            if (line.Substring(0, index).Trim() != variableName) continue;
            return line.Substring(index + 1).Trim();
        }
        return "(未找到)";
    }
}

/// <summary>
/// 临时把"安装记录"移开，使程序在这段时间里找不到安装目录。
///
/// 构造/关闭 MainWindow 会触发 ObscuredPackageDeploymentService 对**真实部署目录**里
/// sorinkg.lua / sorinxs.lua 的改名（启动恢复、退出禁用）。测试不应该动用户的配置文件，
/// 因此测试期间让 TryGetInstallDirectory 返回 null，测试结束（含异常）后原样放回。
/// </summary>
internal sealed class InstallRecordGuard : IDisposable
{
    private readonly string _path;
    private readonly string _backupPath;
    private readonly bool _hidden;

    private InstallRecordGuard(string path, string backupPath, bool hidden)
    {
        _path = path;
        _backupPath = backupPath;
        _hidden = hidden;
    }

    public static InstallRecordGuard Hide()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMacQ", "State");
        var path = Path.Combine(directory, "cache.dat");
        var backupPath = path + ".testsbak";

        try
        {
            if (!File.Exists(path)) return new InstallRecordGuard(path, backupPath, hidden: false);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(path, backupPath);
            return new InstallRecordGuard(path, backupPath, hidden: true);
        }
        catch (IOException)
        {
            return new InstallRecordGuard(path, backupPath, hidden: false);
        }
        catch (UnauthorizedAccessException)
        {
            return new InstallRecordGuard(path, backupPath, hidden: false);
        }
    }

    public void Dispose()
    {
        if (!_hidden) return;
        try
        {
            if (File.Exists(_backupPath) && !File.Exists(_path)) File.Move(_backupPath, _path);
        }
        catch (IOException)
        {
            // 恢复失败时保留 .testsbak 副本，避免数据丢失
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}

/// <summary>
/// 配置文件的禁用/恢复状态机。目标语义：程序关闭后两个配置文件处于禁用状态（鼠标宏读不到），
/// 启动时恢复。这里锁住的是"两份并存时仍然必须生效"——曾经的实现只在目标不存在时才改名，
/// 一旦部署时重新解压出活动文件，禁用与恢复就双双失效。
/// </summary>
internal static class ConfigurationFileStateTests
{
    public static void Run()
    {
        // 只有活动文件 → 禁用后只剩禁用副本，内容随活动文件走
        var directory = TestConfig.CreateWorkDirectory();
        var active = Path.Combine(directory, "sorinkg.lua");
        var disabled = active + ".disabled";
        File.WriteAllText(active, "新内容");
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: true), "禁用操作成功");
        TestRunner.Check(!File.Exists(active), "禁用后活动文件不存在");
        TestRunner.CheckEqual("新内容", File.ReadAllText(disabled), "禁用副本内容来自活动文件");

        // 再次禁用应当幂等
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: true), "重复禁用仍然成功");
        TestRunner.CheckEqual("新内容", File.ReadAllText(disabled), "重复禁用不改变内容");

        // 只有禁用副本 → 恢复后回到活动文件
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: false), "恢复操作成功");
        TestRunner.Check(!File.Exists(disabled), "恢复后禁用副本不存在");
        TestRunner.CheckEqual("新内容", File.ReadAllText(active), "恢复后内容不变");
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: false), "重复恢复仍然成功");

        // 两份并存 → 以活动文件（较新）为准
        File.WriteAllText(active, "活动文件内容");
        File.WriteAllText(disabled, "过期的禁用副本");
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: false), "两份并存时可以恢复");
        TestRunner.CheckEqual("活动文件内容", File.ReadAllText(active), "恢复时保留较新的活动文件");
        TestRunner.Check(!File.Exists(disabled), "恢复时清掉过期的禁用副本");

        File.WriteAllText(active, "活动文件内容");
        File.WriteAllText(disabled, "过期的禁用副本");
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: true), "两份并存时可以禁用");
        TestRunner.Check(!File.Exists(active), "禁用后活动文件不存在");
        TestRunner.CheckEqual("活动文件内容", File.ReadAllText(disabled), "禁用时以较新的活动文件为准，不保留过期副本");

        // 两份都不存在时不应报错
        File.Delete(disabled);
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: true), "两份都不存在时禁用不报错");
        TestRunner.Check(ObscuredPackageDeploymentService.TryApplyConfigurationFileState(active, disabled, disable: false), "两份都不存在时恢复不报错");

        TestConfig.Cleanup();
    }
}

/// <summary>
/// 编码识别：非 UTF-8（GBK/ANSI）的配置必须按原编码读出、按原编码写回，
/// 否则用户的中文注释会被改坏。曾经的行为是"没有 BOM 就一律当 UTF-8"。
/// </summary>
internal static class EncodingTests
{
    public static void Run()
    {
        var directory = TestConfig.CreateWorkDirectory();
        var path = Path.Combine(directory, "sorinkg.lua");
        var comment = "---- 中文注释：M4A1 [SP] ----";

        // 1) 用系统 ANSI 代码页写出的文件，必须按原编码读回
        var ansiBytes = Encoding.Default.GetBytes(comment + "\nM4A1_qq1156777787 = 4\n");
        File.WriteAllBytes(path, ansiBytes);
        var (content, encoding) = FileEncodingService.ReadAllText(path);
        TestRunner.CheckEqual(comment + "\nM4A1_qq1156777787 = 4\n", content, "ANSI 编码文件按原编码读出");
        TestRunner.Check(encoding.CodePage != 65001, "非 UTF-8 文件不会被误判成 UTF-8");

        // 2) 用识别出的编码写回，字节必须完全一致（核心保证：不被偷偷改成 UTF-8）
        AtomicFileWriter.WriteAllText(path, content, encoding);
        TestRunner.Check(File.ReadAllBytes(path).SequenceEqual(ansiBytes), "按原编码写回后字节完全一致");

        // 3) 合法的 UTF-8（无 BOM）按 UTF-8 处理
        var utf8Bytes = new UTF8Encoding(false).GetBytes(comment + "\n");
        File.WriteAllBytes(path, utf8Bytes);
        var utf8Result = FileEncodingService.ReadAllText(path);
        TestRunner.CheckEqual(comment + "\n", utf8Result.Content, "UTF-8（无 BOM）内容正确");
        TestRunner.CheckEqual(65001, utf8Result.Encoding.CodePage, "UTF-8（无 BOM）被识别为 UTF-8");

        // 4) 带 BOM 的 UTF-8 保留 BOM
        var bomBytes = new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(comment)).ToArray();
        File.WriteAllBytes(path, bomBytes);
        TestRunner.Check(FileEncodingService.ReadAllText(path).Encoding.GetPreamble().Length == 3, "带 BOM 的 UTF-8 保留 BOM");
        AtomicFileWriter.WriteAllText(path, comment, new UTF8Encoding(true));
        TestRunner.Check(File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "写回后 BOM 仍在");

        // 5) UTF-16 LE 识别（写入 BOM；无 BOM 的 UTF-16 无法可靠识别，属于已知限制）
        File.WriteAllText(path, comment, Encoding.Unicode);
        var utf16 = FileEncodingService.ReadAllText(path);
        TestRunner.CheckEqual(comment, utf16.Content, "UTF-16 LE 内容正确");
        TestRunner.CheckEqual(1200, utf16.Encoding.CodePage, "UTF-16 LE 被识别");

        TestConfig.Cleanup();
    }
}

/// <summary>测试用的临时配置文件。</summary>
internal sealed class TestConfig
{
    private static readonly List<string> ActiveDirectories = [];

    private TestConfig(string directory, string keyPath, string sensitivityPath)
    {
        Directory = directory;
        KeyPath = keyPath;
        SensitivityPath = sensitivityPath;
    }

    public string Directory { get; }
    public string KeyPath { get; }
    public string SensitivityPath { get; }

    public static TestConfig Create(string keyBindings)
    {
        var directory = CreateWorkDirectory();

        var keyPath = Path.Combine(directory, "sorinkg.lua");
        var sensitivityPath = Path.Combine(directory, "sorinxs.lua");
        File.WriteAllText(keyPath, keyBindings, new UTF8Encoding(false));
        File.WriteAllText(sensitivityPath, "M14_qq1156777787_X = 1\nM14_qq1156777787_Y = 1\nM14_qq1156777787_add_X = 1\nM14_qq1156777787_add_Y = 1\nAKM_qq1156777787_X = 1\nAKM_qq1156777787_Y = 1\nAKM_qq1156777787_add_X = 1\nAKM_qq1156777787_add_Y = 1\n", new UTF8Encoding(false));
        return new TestConfig(directory, keyPath, sensitivityPath);
    }

    /// <summary>
    /// 优先在测试程序自己的目录下建工作目录（构建产物目录一定可写），
    /// 某些受限环境下系统临时目录不可写，此时再退回临时目录。
    /// </summary>
    public static string CreateWorkDirectory()
    {
        var candidates = new[] { AppDomain.CurrentDomain.BaseDirectory, Path.GetTempPath() };
        foreach (var root in candidates)
        {
            try
            {
                var directory = Path.Combine(root, "amacq-tests-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(directory);
                ActiveDirectories.Add(directory);
                return directory;
            }
            catch (UnauthorizedAccessException)
            {
                // 换下一个候选目录
            }
            catch (IOException)
            {
                // 换下一个候选目录
            }
        }

        throw new InvalidOperationException("找不到可写的工作目录");
    }

    /// <summary>加载一份临时配置并返回 ViewModel（用于不涉及界面的用例）。</summary>
    public static MainWindowViewModel Load(string keyBindings)
    {
        var config = Create(keyBindings);
        var viewModel = new MainWindowViewModel();
        viewModel.Load(config.KeyPath, config.SensitivityPath);
        return viewModel;
    }

    /// <summary>把主窗口的配置路径指向临时文件，避免触碰真实部署目录。</summary>
    public static void PointMainWindowAt(MainWindow window, TestConfig config)
    {
        var type = typeof(MainWindow);
        type.GetField("_keyBindingsPath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, config.KeyPath);
        type.GetField("_sensitivityPath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, config.SensitivityPath);
    }

    public static void Cleanup()
    {
        foreach (var directory in ActiveDirectories)
        {
            try
            {
                if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // 清理失败不影响测试结论
            }
        }
        ActiveDirectories.Clear();
    }
}
