using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 屏幕边缘吸附与平滑滑出/隐藏停靠引擎服务
    /// </summary>
    public class EdgeDockService : IDisposable
    {
        private readonly Window _window;
        private readonly AppSettings _settings;
        private readonly DispatcherTimer _mouseCheckTimer;
        private HwndSource? _hwndSource;

        public DockEdge CurrentDock { get; private set; } = DockEdge.None;
        public bool IsHidden { get; private set; } = false;
        public bool IsAnimating { get; private set; } = false;
        public bool IsDragging { get; private set; } = false;
        public bool IsPinned { get; set; } = false;

        private const double SensorVisibleThickness = 4.0;
        private int _mouseOutCount = 0;
        private bool _hasMouseEnteredSinceSlideOut = false;

        public event Action<DockEdge>? DockChanged;
        public event Action<bool>? HiddenStateChanged;

        /// <summary>
        /// 构造函数，初始化贴边停靠引擎
        /// </summary>
        public EdgeDockService(Window window, AppSettings settings)
        {
            _window = window;
            _settings = settings;
            _mouseCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _mouseCheckTimer.Tick += OnMouseCheckTick;
            _window.Loaded += OnWindowLoaded;
            _window.MouseEnter += OnWindowMouseEnter;
        }

        /// <summary>
        /// 窗口加载完成回调，挂载 Win32 消息钩子并允许管理员权限下的拖拽穿透
        /// </summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(_window);
            IntPtr hWnd = helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(hWnd);
            _hwndSource?.AddHook(WndProc);

            // Allow Explorer drag-and-drop through UIPI even when elevated
            NativeMethods.ChangeWindowMessageFilterEx(hWnd, NativeMethods.WM_DROPFILES, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
            NativeMethods.ChangeWindowMessageFilterEx(hWnd, NativeMethods.WM_COPYDATA, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);
            NativeMethods.ChangeWindowMessageFilterEx(hWnd, NativeMethods.WM_COPYGLOBALDATA, NativeMethods.MSGFLT_ALLOW, IntPtr.Zero);

            _mouseCheckTimer.Start();
            CheckInitialDock();
        }

        /// <summary>
        /// Win32 消息循环处理，监听原生窗口移动事件
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case NativeMethods.WM_ENTERSIZEMOVE:
                    BeginDrag();
                    break;
                case NativeMethods.WM_EXITSIZEMOVE:
                    EndDrag();
                    break;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 检查并恢复上次退出时所保存的初始停靠状态
        /// </summary>
        public void CheckInitialDock()
        {
            if (!_settings.EnableEdgeDocking) return;
            if (_settings.LastDockEdge != DockEdge.None)
            {
                SetDock(_settings.LastDockEdge);
            }
        }

        /// <summary>
        /// 解除停靠状态，使窗口恢复为自由浮动窗口
        /// </summary>
        public void Undock()
        {
            SetDock(DockEdge.None);
            SlideOutImmediate();
            _hasMouseEnteredSinceSlideOut = false;
            _mouseOutCount = 0;
        }

        /// <summary>
        /// 用户开始拖拽窗口时的准备工作：解除动画时钟、同步内核真实坐标
        /// </summary>
        public void BeginDrag()
        {
            if (IsDragging) return;
            IsDragging = true;
            _mouseCheckTimer.Stop();

            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            var helper = new WindowInteropHelper(_window);
            IntPtr hWnd = helper.Handle;
            if (hWnd != IntPtr.Zero && NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                var dpi = VisualTreeHelper.GetDpi(_window);
                double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                _window.Left = rect.Left / dpiX;
                _window.Top = rect.Top / dpiY;
            }

            IsAnimating = false;
            if (IsHidden)
            {
                SlideOutImmediate();
            }
        }

        /// <summary>
        /// 用户松开鼠标结束拖拽：恢复定时器并检测是否贴边
        /// </summary>
        public void EndDrag()
        {
            if (!IsDragging) return;
            IsDragging = false;
            _mouseCheckTimer.Start();
            CheckAndSnapToEdge();
        }

        /// <summary>
        /// 检测当前物理窗口坐标是否贴合屏幕边缘（容差 8px），决定停靠或自由浮动
        /// </summary>
        public void CheckAndSnapToEdge()
        {
            if (!_settings.EnableEdgeDocking)
            {
                SetDock(DockEdge.None);
                return;
            }

            var helper = new WindowInteropHelper(_window);
            IntPtr hWnd = helper.Handle;
            var dpi = VisualTreeHelper.GetDpi(_window);
            double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            double winLeft = _window.Left;
            double winTop = _window.Top;
            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            double actualHeight = _window.ActualHeight > 0 ? _window.ActualHeight : _window.Height;

            if (hWnd != IntPtr.Zero && NativeMethods.GetWindowRect(hWnd, out var winRect))
            {
                winLeft = winRect.Left / dpiX;
                winTop = winRect.Top / dpiY;
                actualWidth = (winRect.Right - winRect.Left) / dpiX;
                actualHeight = (winRect.Bottom - winRect.Top) / dpiY;

                _window.BeginAnimation(Window.LeftProperty, null);
                _window.BeginAnimation(Window.TopProperty, null);
                _window.Left = winLeft;
                _window.Top = winTop;
            }

            var workArea = GetCurrentWorkArea();
            double winRight = winLeft + actualWidth;
            const double SnapTolerance = 8.0;
            DockEdge targetDock = DockEdge.None;

            if (winLeft <= workArea.Left + SnapTolerance)
            {
                targetDock = DockEdge.Left;
            }
            else if (winRight >= workArea.Right - SnapTolerance)
            {
                targetDock = DockEdge.Right;
            }
            else if (winTop <= workArea.Top + SnapTolerance)
            {
                targetDock = DockEdge.Top;
            }

            SetDock(targetDock);
        }

        /// <summary>
        /// 设置当前停靠边缘状态，联动更新吸附位置、置顶属性及事件
        /// </summary>
        private void SetDock(DockEdge dock)
        {
            CurrentDock = dock;
            _settings.LastDockEdge = dock;

            if (dock != DockEdge.None)
            {
                SnapToEdge(dock);
                if (_window is MainWindow mw)
                {
                    mw.ApplyTopmostState(true);
                }
                else
                {
                    _window.Topmost = true;
                }
            }
            else
            {
                _window.BeginAnimation(Window.LeftProperty, null);
                _window.BeginAnimation(Window.TopProperty, null);
                IsHidden = false;
                IsAnimating = false;

                if (_window is MainWindow mw)
                {
                    mw.ApplyTopmostState(_settings.AlwaysOnTop);
                }
                else
                {
                    _window.Topmost = _settings.AlwaysOnTop;
                }
            }

            DockChanged?.Invoke(dock);
        }

        /// <summary>
        /// 物理贴靠到指定边缘边界坐标
        /// </summary>
        private void SnapToEdge(DockEdge edge)
        {
            var workArea = GetCurrentWorkArea();
            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;

            switch (edge)
            {
                case DockEdge.Top:
                    _window.Top = workArea.Top;
                    break;
                case DockEdge.Left:
                    _window.Left = workArea.Left;
                    break;
                case DockEdge.Right:
                    _window.Left = workArea.Right - actualWidth;
                    break;
            }
        }

        /// <summary>
        /// 鼠标划入边缘感应区时自动滑出展开窗口
        /// </summary>
        private void OnWindowMouseEnter(object sender, MouseEventArgs e)
        {
            if (IsHidden && !IsDragging)
            {
                SlideOut();
            }
        }

        /// <summary>
        /// 定时器轮询：全局检测光标位置，当鼠标离开停靠窗口延时后自动滑入隐藏
        /// </summary>
        private void OnMouseCheckTick(object? sender, EventArgs e)
        {
            if (CurrentDock == DockEdge.None || !_settings.EnableEdgeAutoHide || IsPinned || IsAnimating || IsDragging)
            {
                return;
            }

            if (Mouse.LeftButton == MouseButtonState.Pressed || Mouse.RightButton == MouseButtonState.Pressed)
            {
                _mouseOutCount = 0;
                return;
            }

            if (!NativeMethods.GetCursorPos(out var pt)) return;

            var source = PresentationSource.FromVisual(_window);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            double mouseX = pt.X / dpiX;
            double mouseY = pt.Y / dpiY;

            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            double actualHeight = _window.ActualHeight > 0 ? _window.ActualHeight : _window.Height;

            bool isMouseInWindow = mouseX >= _window.Left && mouseX <= _window.Left + actualWidth &&
                                   mouseY >= _window.Top && mouseY <= _window.Top + actualHeight;

            if (isMouseInWindow)
            {
                _mouseOutCount = 0;
                _hasMouseEnteredSinceSlideOut = true;
                if (IsHidden)
                {
                    SlideOut();
                }
            }
            else
            {
                if (!IsHidden)
                {
                    _mouseOutCount++;
                    int thresholdTicks = Math.Max(1, _settings.AutoHideDelayMs / 100);
                    int effectiveThreshold = _hasMouseEnteredSinceSlideOut ? thresholdTicks : Math.Max(thresholdTicks, 50);

                    if (_mouseOutCount >= effectiveThreshold)
                    {
                        SlideIn();
                    }
                }
            }
        }

        /// <summary>
        /// 平滑动画：将窗口滑入屏幕边缘隐藏，保留 4px 细长感应边缘
        /// </summary>
        public void SlideIn()
        {
            if (IsHidden || IsAnimating || IsDragging || CurrentDock == DockEdge.None) return;

            var workArea = GetCurrentWorkArea();
            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            double actualHeight = _window.ActualHeight > 0 ? _window.ActualHeight : _window.Height;
            IsAnimating = true;

            var anim = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            double targetPos;
            DependencyProperty propToAnimate;

            switch (CurrentDock)
            {
                case DockEdge.Top:
                    targetPos = workArea.Top - actualHeight + SensorVisibleThickness;
                    propToAnimate = Window.TopProperty;
                    break;
                case DockEdge.Left:
                    targetPos = workArea.Left - actualWidth + SensorVisibleThickness;
                    propToAnimate = Window.LeftProperty;
                    break;
                case DockEdge.Right:
                    targetPos = workArea.Right - SensorVisibleThickness;
                    propToAnimate = Window.LeftProperty;
                    break;
                default:
                    IsAnimating = false;
                    return;
            }

            anim.To = targetPos;
            anim.Completed += (s, e) =>
            {
                _window.BeginAnimation(propToAnimate, null);
                if (propToAnimate == Window.LeftProperty) _window.Left = targetPos;
                else _window.Top = targetPos;

                IsAnimating = false;
                IsHidden = true;
                HiddenStateChanged?.Invoke(true);
            };

            _window.BeginAnimation(propToAnimate, anim);
        }

        /// <summary>
        /// 平滑动画：将停靠在边缘的窗口滑出屏幕供用户交互
        /// </summary>
        public void SlideOut()
        {
            if (!IsHidden || IsAnimating || CurrentDock == DockEdge.None) return;

            _hasMouseEnteredSinceSlideOut = false;
            _mouseOutCount = 0;

            var workArea = GetCurrentWorkArea();
            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;
            IsAnimating = true;

            var anim = new DoubleAnimation
            {
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            double targetPos;
            DependencyProperty propToAnimate;

            switch (CurrentDock)
            {
                case DockEdge.Top:
                    targetPos = workArea.Top;
                    propToAnimate = Window.TopProperty;
                    break;
                case DockEdge.Left:
                    targetPos = workArea.Left;
                    propToAnimate = Window.LeftProperty;
                    break;
                case DockEdge.Right:
                    targetPos = workArea.Right - actualWidth;
                    propToAnimate = Window.LeftProperty;
                    break;
                default:
                    IsAnimating = false;
                    return;
            }

            anim.To = targetPos;
            anim.Completed += (s, e) =>
            {
                _window.BeginAnimation(propToAnimate, null);
                if (propToAnimate == Window.LeftProperty) _window.Left = targetPos;
                else _window.Top = targetPos;

                IsAnimating = false;
                IsHidden = false;
                (_window as MainWindow)?.ApplyTopmostState(CurrentDock != DockEdge.None ? true : _settings.AlwaysOnTop);
                HiddenStateChanged?.Invoke(false);
            };

            _window.BeginAnimation(propToAnimate, anim);
        }

        /// <summary>
        /// 瞬时无动画滑出窗口，立即完全显示
        /// </summary>
        public void SlideOutImmediate()
        {
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            var workArea = GetCurrentWorkArea();
            double actualWidth = _window.ActualWidth > 0 ? _window.ActualWidth : _window.Width;

            switch (CurrentDock)
            {
                case DockEdge.Top:
                    _window.Top = workArea.Top;
                    break;
                case DockEdge.Left:
                    _window.Left = workArea.Left;
                    break;
                case DockEdge.Right:
                    _window.Left = workArea.Right - actualWidth;
                    break;
            }

            IsHidden = false;
            IsAnimating = false;
            (_window as MainWindow)?.ApplyTopmostState(CurrentDock != DockEdge.None ? true : _settings.AlwaysOnTop);
            HiddenStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 切换窗口的显示与隐藏状态
        /// </summary>
        public void ToggleShowHide()
        {
            if (CurrentDock != DockEdge.None && _settings.EnableEdgeAutoHide)
            {
                if (IsHidden) SlideOut();
                else SlideIn();
            }
            else
            {
                if (_window.Visibility == Visibility.Visible)
                {
                    _window.Hide();
                }
                else
                {
                    _window.Show();
                    _window.Activate();
                    (_window as MainWindow)?.ApplyTopmostState(CurrentDock != DockEdge.None ? true : _settings.AlwaysOnTop);
                }
            }
        }

        /// <summary>
        /// 获取当前窗口所在物理显示器的有效工作区域（排除任务栏）
        /// </summary>
        private Rect GetCurrentWorkArea()
        {
            try
            {
                var helper = new WindowInteropHelper(_window);
                IntPtr hWnd = helper.Handle;
                if (hWnd != IntPtr.Zero)
                {
                    IntPtr hMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                    if (hMonitor != IntPtr.Zero)
                    {
                        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                        if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                        {
                            var dpi = VisualTreeHelper.GetDpi(_window);
                            double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                            double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                            return new Rect(mi.rcWork.Left / dpiX, mi.rcWork.Top / dpiY, mi.rcWork.Width / dpiX, mi.rcWork.Height / dpiY);
                        }
                    }
                }
            }
            catch { }

            return SystemParameters.WorkArea;
        }

        /// <summary>
        /// 释放资源并注销消息钩子
        /// </summary>
        public void Dispose()
        {
            _mouseCheckTimer.Stop();
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
        }
    }
}
