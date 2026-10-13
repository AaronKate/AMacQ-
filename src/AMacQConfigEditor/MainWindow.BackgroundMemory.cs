using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using MediaAnimation = System.Windows.Media.Animation;

namespace AMacQConfigEditor;

/// <summary>
/// 隐藏到托盘后的内存让渡。
///
/// 网吧与低配机器上，本程序长期挂在托盘里，同时还有游戏在跑，物理内存很紧张。窗口隐藏后
/// 这里做两件事：停掉常驻动画（否则每帧都在摸页面，刚换出去的又会被摸回来），以及在空闲
/// 几秒后回收托管垃圾并把工作集换出，把物理内存真正还给系统。
///
/// 换出的是工作集，私有字节（提交）不变：省下来的是物理内存，不是占用总量。代价是被换出
/// 的页面在下次交互时通过缺页读回，会有一次轻微顿挫——所以只在隐藏且确实空闲时才做。
/// </summary>
public partial class MainWindow
{
    /// <summary>隐藏后等待多久再换出。误点最小化又马上打开时，不至于先付一次缺页的钱。</summary>
    private static readonly TimeSpan BackgroundTrimDelay = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _backgroundTrimTimer = new() { Interval = BackgroundTrimDelay };
    private MediaAnimation.Storyboard? _backgroundGlow;
    private bool _glowClockPaused;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

    /// <summary>
    /// 启动背景光晕动画。以 isControllable 方式启动，之后按"可见且持有焦点"启停。
    /// </summary>
    private void StartBackgroundGlow()
    {
        _backgroundGlow ??= (MediaAnimation.Storyboard)FindResource("BackgroundGlowAnimation");

        // SnapshotAndReplace + isControllable：保留原有启动行为，同时让这副时钟可以被暂停。
        _backgroundGlow.Begin(this, MediaAnimation.HandoffBehavior.SnapshotAndReplace, true);
        _glowClockPaused = false;

        // 常驻动画即使限帧后仍在持续产生每帧分配（变换对象克隆），而它的观感价值只在
        // 用户盯着主窗口时存在。焦点一离开（快速切换窗口、帮助对话框、其它程序）就暂停，
        // 回到主窗口再恢复——用户报告的"打开枪械选择界面时内存每几秒涨 0.1MB"即由此而来。
        Activated += (_, _) => UpdateGlowState();
        Deactivated += (_, _) => UpdateGlowState();
        UpdateGlowState();
    }

    /// <summary>光晕时钟的启停条件：主窗口可见且持有焦点。其它情况一律暂停。</summary>
    private void UpdateGlowState()
    {
        if (_backgroundGlow is null) return;

        var shouldRun = IsVisible && IsActive;
        if (shouldRun == !_glowClockPaused) return;

        if (shouldRun) _backgroundGlow.Resume(this);
        else _backgroundGlow.Pause(this);
        _glowClockPaused = !shouldRun;
    }

    /// <summary>
    /// 进入后台（隐藏到托盘）：停住常驻动画，再排队做一次内存让渡。
    /// </summary>
    private void EnterBackgroundMode()
    {
        UpdateGlowState();

        _backgroundTrimTimer.Stop();
        _backgroundTrimTimer.Start();
    }

    /// <summary>回到前台：取消待执行的让渡；动画时钟随后由 Activated 事件按焦点状态恢复。</summary>
    private void ExitBackgroundMode()
    {
        _backgroundTrimTimer.Stop();
        UpdateGlowState();
    }

    private void TrimBackgroundFootprint()
    {
        // 悬浮提示和快速切换窗口是"后台里还会临时露脸"的界面：这时候换出页面，用户下一步
        // 操作就得等缺页，得不偿失。下一次隐藏时会重新排队。
        if (_sensitivityOverlay.IsVisible || _quickSwitchWindow?.IsVisible == true) return;

        // 先回收，再换出。部署解压与配置解析留下的垃圾如果不先清掉，随后又会被换回内存，
        // 等于白换一次；LOH 一次性压缩是为了把大块空隙还给系统。
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        // minimum/maximum 传 -1 表示"尽量换出"：页面进入待机列表，物理内存归还系统。
        SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
    }
}
