using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch;

/// <summary>
/// 快快启动 (KuaiKuai Launch) 应用程序入口与生命周期管理（支持单实例互斥与重复启动唤醒）
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Local\KuaiKuaiLaunch_SingleInstance_Mutex_9A8F6C2E4D7B4E38";
    private const string EventName = @"Local\KuaiKuaiLaunch_Wakeup_Event_9A8F6C2E4D7B4E38";

    private static Mutex? _mutex;
    private static EventWaitHandle? _wakeupEvent;
    private static RegisteredWaitHandle? _waitHandleRegistration;
    private static bool _hasMutexOwnership = false;

    /// <summary>
    /// 应用程序启动拦截：单实例检查与主窗口初始化
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        bool createdNew;
        try
        {
            _mutex = new Mutex(true, MutexName, out createdNew);
        }
        catch (AbandonedMutexException)
        {
            // 上一个实例非正常退出导致互斥体未释放，当前实例接管所有权
            createdNew = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Mutex creation failed: {ex.Message}");
            createdNew = true;
        }

        if (!createdNew)
        {
            // 已有实例在运行：向其发送唤醒信号，拉至前台，并安全退出当前实例
            NotifyExistingInstance();
            Shutdown();
            return;
        }

        _hasMutexOwnership = true;

        // 注册跨进程唤醒监听
        SetupWakeupListener();

        base.OnStartup(e);

        // 创建并显示主窗口
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// 初始化后台跨进程唤醒监听器
    /// </summary>
    private void SetupWakeupListener()
    {
        try
        {
            _wakeupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

            _waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(
                _wakeupEvent,
                (state, timedOut) =>
                {
                    if (!timedOut)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.WakeupAndActivate();
                            }
                        });
                    }
                },
                null,
                -1,
                false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] SetupWakeupListener failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 通知已有实例唤醒并尝试恢复至屏幕最前台
    /// </summary>
    private static void NotifyExistingInstance()
    {
        // 1. 通过命名事件触发主实例内部唤醒逻辑
        try
        {
            if (EventWaitHandle.TryOpenExisting(EventName, out var wakeupEvent))
            {
                wakeupEvent.Set();
                wakeupEvent.Dispose();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Notify via EventWaitHandle failed: {ex.Message}");
        }

        // 2. 通过 Win32 API 辅助将已有进程窗口置于前台
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processName = currentProcess.ProcessName;
            var existingProcesses = Process.GetProcessesByName(processName)
                .Where(p => p.Id != currentProcess.Id)
                .ToList();

            foreach (var process in existingProcesses)
            {
                NativeMethods.AllowSetForegroundWindow((uint)process.Id);

                IntPtr hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    if (NativeMethods.IsIconic(hWnd))
                    {
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                    }
                    else
                    {
                        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
                    }
                    NativeMethods.SetForegroundWindow(hWnd);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Bring existing window to front failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用程序退出：释放互斥体与跨进程事件监听资源
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _waitHandleRegistration?.Unregister(null);
            _waitHandleRegistration = null;

            _wakeupEvent?.Dispose();
            _wakeupEvent = null;

            if (_hasMutexOwnership && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
                _mutex.Dispose();
                _mutex = null;
                _hasMutexOwnership = false;
            }
        }
        catch { }

        base.OnExit(e);
    }
}

