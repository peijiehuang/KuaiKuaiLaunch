using System;
using System.Windows;
using System.Windows.Interop;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 全局系统热键注册与监听服务（基于 RegisterHotKey / WM_HOTKEY）
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private const int HOTKEY_ID = 9001;
        private IntPtr _hWnd = IntPtr.Zero;
        private HwndSource? _source;
        private bool _isRegistered = false;

        public event Action? HotkeyPressed;

        /// <summary>
        /// 绑定主窗口句柄并挂载消息监听钩子
        /// </summary>
        /// <param name="window">WPF 目标窗口</param>
        public void Initialize(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(HwndHook);
        }

        /// <summary>
        /// 注册全局热键（自动包含 MOD_NOREPEAT 防粘滞重复触发）
        /// </summary>
        /// <param name="modifiers">控制修饰键（MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_WIN）</param>
        /// <param name="virtualKey">虚拟键码（VK_*）</param>
        /// <returns>注册是否成功</returns>
        public bool Register(uint modifiers, uint virtualKey)
        {
            Unregister();
            if (_hWnd == IntPtr.Zero) return false;
            _isRegistered = NativeMethods.RegisterHotKey(_hWnd, HOTKEY_ID, modifiers | NativeMethods.MOD_NOREPEAT, virtualKey);
            return _isRegistered;
        }

        /// <summary>
        /// 注销当前已注册的全局热键
        /// </summary>
        public void Unregister()
        {
            if (_isRegistered && _hWnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hWnd, HOTKEY_ID);
                _isRegistered = false;
            }
        }

        /// <summary>
        /// 拦截窗口消息，识别 WM_HOTKEY 并派发 HotkeyPressed 事件
        /// </summary>
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 释放资源并移除窗口钩子
        /// </summary>
        public void Dispose()
        {
            Unregister();
            if (_source != null)
            {
                _source.RemoveHook(HwndHook);
                _source = null;
            }
        }
    }
}
