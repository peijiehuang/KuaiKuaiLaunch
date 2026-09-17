using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 全局底层鼠标钩子监听服务（基于 WH_MOUSE_LL 实现鼠标中键滚轮点击全局唤醒/隐藏主面板）
    /// </summary>
    public class MouseHookService : IDisposable
    {
        private static NativeMethods.LowLevelMouseProc? _staticProc;
        private IntPtr _hookId = IntPtr.Zero;

        public event Action? MiddleMouseClicked;

        public bool IsRunning => _hookId != IntPtr.Zero;

        /// <summary>
        /// 安装并启动全局底层鼠标钩子
        /// </summary>
        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            // 保持静态代理引用，防止委托被 GC 垃圾回收导致钩子失效崩溃
            _staticProc = HookCallback;

            using var process = Process.GetCurrentProcess();
            using var module = process.MainModule;
            IntPtr hModule = NativeMethods.GetModuleHandle(module?.ModuleName);

            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _staticProc, hModule, 0);
            if (_hookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[MouseHookService] SetWindowsHookEx failed, error: {err}");
            }
        }

        /// <summary>
        /// 卸载并注销全局鼠标钩子
        /// </summary>
        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 底层鼠标钩子消息过滤回调函数（捕获 WM_MBUTTONUP 并转发给 Dispatcher UI 线程）
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_MBUTTONUP)
            {
                var app = System.Windows.Application.Current;
                if (app != null && !app.Dispatcher.HasShutdownStarted)
                {
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        MiddleMouseClicked?.Invoke();
                    });
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 释放资源并停止鼠标监听
        /// </summary>
        public void Dispose()
        {
            Stop();
            _staticProc = null;
        }
    }
}
