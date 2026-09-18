using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;
using KuaiKuaiLaunch.Services.Win32;
using KuaiKuaiLaunch.ViewModels;
using KuaiKuaiLaunch.Views.Dialogs;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace KuaiKuaiLaunch
{
    /// <summary>
    /// 快快启动 (KuaiKuai Launch) 主窗口交互逻辑（致敬音速启动）
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private readonly StorageService _storageService;
        private readonly LnkParserService _lnkParserService;
        private readonly IconExtractorService _iconExtractorService;
        private readonly LauncherService _launcherService;
        private readonly AutoStartService _autoStartService;
        private readonly DesktopShortcutService _desktopShortcutService;

        private readonly MainViewModel _viewModel;
        private readonly EdgeDockService _edgeDockService;
        private readonly HotkeyService _hotkeyService;
        private readonly MouseHookService _mouseHookService;
        private readonly TrayIconManager _trayIconManager;

        private bool _isRealExit = false;

        /// <summary>
        /// 构造函数，初始化核心服务、ViewModel、托盘与窗口事件
        /// </summary>
        public MainWindow()
        {
            _storageService = new StorageService();
            _lnkParserService = new LnkParserService(_storageService);
            _iconExtractorService = new IconExtractorService(_storageService);
            _launcherService = new LauncherService(_storageService);
            _autoStartService = new AutoStartService();
            _desktopShortcutService = new DesktopShortcutService();

            _viewModel = new MainViewModel(_storageService, _lnkParserService, _iconExtractorService, _launcherService, _autoStartService);
            DataContext = _viewModel;
            InitializeComponent();

            ApplyTheme(_viewModel.Settings.Theme);
            ApplyWindowPlacement();

            _edgeDockService = new EdgeDockService(this, _viewModel.Settings);
            _hotkeyService = new HotkeyService();
            _mouseHookService = new MouseHookService();
            _trayIconManager = new TrayIconManager(this, _viewModel, _edgeDockService);

            if (_viewModel.Settings.EnableMiddleClickWakeup) _mouseHookService.Start();
            _mouseHookService.MiddleMouseClicked += OnMiddleMouseClicked;

            WireViewModelEvents();
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            SizeChanged += (s, e) => SaveWindowPlacement();
        }

        /// <summary>
        /// 窗口 Win32 句柄创建完成，第一时间注入 DWM 沉浸式深色模式与 Fluent 材质
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ApplyTheme(_viewModel.Settings.Theme, force: true);
        }

        /// <summary>
        /// 底层硬件级设置窗口置顶状态（调用 Win32 SetWindowPos HWND_TOPMOST / HWND_NOTOPMOST）
        /// </summary>
        /// <param name="isTopmost">是否置顶</param>
        public void ApplyTopmostState(bool isTopmost)
        {
            Topmost = isTopmost;
            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetWindowPos(
                        helper.Handle,
                        isTopmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
                        0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
                }
            }
            catch { }
        }

        /// <summary>
        /// 窗口加载完成：注册全局热键与置顶状态并确保主题材质生效
        /// </summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _hotkeyService.Initialize(this);
            RegisterGlobalHotkey();
            ApplyTopmostState(_viewModel.Settings.AlwaysOnTop);
            ApplyTheme(_viewModel.Settings.Theme, force: true);

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                CheckAndPromptDesktopShortcut();
            });
        }

        /// <summary>
        /// 检查桌面是否存在快捷方式，若未检测到则提示用户创建
        /// </summary>
        private void CheckAndPromptDesktopShortcut()
        {
            try
            {
                if (!_viewModel.Settings.CheckDesktopShortcutOnStartup) return;

                if (!_desktopShortcutService.HasDesktopShortcut())
                {
                    var result = MessageBox.Show(
                        this,
                        "检测到桌面尚未创建“快快启动”快捷方式，是否立即在桌面创建快捷方式？",
                        "创建桌面快捷方式",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        bool created = _desktopShortcutService.CreateDesktopShortcut();
                        if (created)
                        {
                            MessageBox.Show(
                                this,
                                "桌面快捷方式已成功创建！",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show(
                                this,
                                "创建桌面快捷方式失败，请检查桌面目录权限。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] CheckAndPromptDesktopShortcut failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 应用深色、浅色或跟随系统 Fluent 主题
        /// </summary>
        private void ApplyTheme(string theme, bool force = false)
        {
            try
            {
                Wpf.Ui.Appearance.ApplicationTheme targetTheme = theme switch
                {
                    "Light" => Wpf.Ui.Appearance.ApplicationTheme.Light,
                    "Dark" => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Dark
                };

                if (force)
                {
                    // 强制清空/重置缓存以确保即使当前已是 Dark，也能强制将新窗口句柄刷入深色主题与DWM材质
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                        targetTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark 
                            ? Wpf.Ui.Appearance.ApplicationTheme.Light 
                            : Wpf.Ui.Appearance.ApplicationTheme.Dark,
                        Wpf.Ui.Controls.WindowBackdropType.None,
                        false);
                }

                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    targetTheme, 
                    Wpf.Ui.Controls.WindowBackdropType.Acrylic, 
                    true);

                // 针对当前窗口显式应用主题与样式
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

                // 通过 Win32 DWM API 确保窗口启用沉浸式暗黑模式 (DWMWA_USE_IMMERSIVE_DARK_MODE)
                ApplyImmersiveDarkMode(targetTheme == Wpf.Ui.Appearance.ApplicationTheme.Dark);
            }
            catch { }
        }

        /// <summary>
        /// 通过系统底层 Win32 DWM API 确保启用沉浸式暗黑模式 (DWMWA_USE_IMMERSIVE_DARK_MODE)
        /// </summary>
        private void ApplyImmersiveDarkMode(bool isDark)
        {
            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    int useDark = isDark ? 1 : 0;
                    NativeMethods.DwmSetWindowAttribute(helper.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
                    NativeMethods.DwmSetWindowAttribute(helper.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
                }
            }
            catch { }
        }

        /// <summary>
        /// 还原上次关闭时记录的窗口大小与屏幕绝对坐标位置
        /// </summary>
        private void ApplyWindowPlacement()
        {
            var s = _viewModel.Settings;
            if (s.WindowWidth >= 300) Width = s.WindowWidth;
            if (s.WindowHeight >= 400) Height = s.WindowHeight;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            if (s.WindowLeft >= 0 && s.WindowLeft < screenW - 100) Left = s.WindowLeft;
            else Left = Math.Max(40, (screenW - Width) / 2);

            if (s.WindowTop >= 0 && s.WindowTop < screenH - 100) Top = s.WindowTop;
            else Top = Math.Max(40, (screenH - Height) / 2 - 40);

            ApplyTopmostState(s.AlwaysOnTop);
        }

        /// <summary>
        /// 将当前窗口位置及尺寸安全写回 settings.json 持久化
        /// </summary>
        private void SaveWindowPlacement()
        {
            if (WindowState == WindowState.Normal && !_edgeDockService.IsHidden && !_edgeDockService.IsAnimating)
            {
                var s = _viewModel.Settings;
                s.WindowLeft = Left;
                s.WindowTop = Top;
                s.WindowWidth = ActualWidth;
                s.WindowHeight = ActualHeight;
                _storageService.SaveSettings(s);
            }
        }

        /// <summary>
        /// 注册设置中所配置的系统全局唤醒热键
        /// </summary>
        private void RegisterGlobalHotkey()
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.Register(_viewModel.Settings.HotkeyModifiers, _viewModel.Settings.HotkeyVirtualKey);
        }

        /// <summary>
        /// 全局热键按下回调：切换显示或滑出隐藏
        /// </summary>
        private void OnHotkeyPressed()
        {
            if (Visibility == Visibility.Visible && !_edgeDockService.IsHidden)
            {
                Hide();
            }
            else
            {
                if (_edgeDockService.CurrentDock != DockEdge.None)
                {
                    _edgeDockService.SlideOut();
                    Show();
                    Activate();
                    ApplyTopmostState(true);
                }
                else
                {
                    Show();
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Activate();
                    ApplyTopmostState(_viewModel.Settings.AlwaysOnTop);
                }
            }
        }

        /// <summary>
        /// 绑定 ViewModel 的弹窗与状态联动事件
        /// </summary>
        private void WireViewModelEvents()
        {
            _viewModel.RequestShowItemEditDialog += (item, isNew, callback) =>
            {
                var dialogVm = new ItemEditViewModel(item, isNew, _storageService, _iconExtractorService);
                var dialog = new ItemEditDialog(dialogVm) { Owner = this };
                dialog.ShowDialog();
                callback(dialogVm.DialogResult);
            };

            _viewModel.RequestShowCategoryEditDialog += (category, isNew, callback) =>
            {
                var dialogVm = new CategoryEditViewModel(category, isNew);
                var dialog = new CategoryEditDialog(dialogVm) { Owner = this };
                dialog.ShowDialog();
                callback(dialogVm.DialogResult);
            };

            _viewModel.RequestShowSettingsDialog += (settings, callback) =>
            {
                var dialogVm = new SettingsViewModel(settings, _autoStartService);
                var dialog = new SettingsWindow(dialogVm) { Owner = this };
                dialog.ShowDialog();
                if (dialogVm.DialogResult)
                {
                    ApplyTopmostState(settings.AlwaysOnTop);
                    _viewModel.IsAlwaysOnTop = settings.AlwaysOnTop;
                    ApplyTheme(settings.Theme, force: true);
                    RegisterGlobalHotkey();

                    if (settings.EnableMiddleClickWakeup) _mouseHookService.Start();
                    else _mouseHookService.Stop();

                    _edgeDockService.CheckInitialDock();
                    callback(true);
                }
            };

            _viewModel.RequestUpdateTopmost += ApplyTopmostState;
            _viewModel.RequestSlideInOrHide += () =>
            {
                if (_edgeDockService.CurrentDock != DockEdge.None && _viewModel.Settings.EnableEdgeAutoHide)
                {
                    _edgeDockService.SlideIn();
                }
                else
                {
                    Hide();
                }
            };
        }

        /// <summary>
        /// 鼠标中键唤醒：在当前光标物理位置呼出主面板（解除停靠并以自由浮动状态呈现）
        /// </summary>
        public void SummonAtMouseCursor()
        {
            _edgeDockService.Undock();

            NativeMethods.POINT pt;
            if (!NativeMethods.GetCursorPos(out pt))
            {
                pt = new NativeMethods.POINT
                {
                    X = (int)(SystemParameters.PrimaryScreenWidth / 2),
                    Y = (int)(SystemParameters.PrimaryScreenHeight / 2)
                };
            }

            var source = PresentationSource.FromVisual(this);
            double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            double cursorX = pt.X / dpiX;
            double cursorY = pt.Y / dpiY;

            var hMonitor = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            NativeMethods.GetMonitorInfo(hMonitor, ref mi);

            double workLeft = mi.rcWork.Left / dpiX;
            double workTop = mi.rcWork.Top / dpiY;
            double workRight = mi.rcWork.Right / dpiX;
            double workBottom = mi.rcWork.Bottom / dpiY;

            double targetW = ActualWidth > 0 ? ActualWidth : Width;
            double targetH = ActualHeight > 0 ? ActualHeight : Height;
            if (targetW < 300) targetW = 530;
            if (targetH < 300) targetH = 680;

            // 鼠标中键唤醒：将软件窗口标题栏标红区域（闪电图标与“快快启动”标题处）精准对齐到鼠标当前坐标
            const double anchorOffsetX = 70; // 标题栏标红区域水平对齐中心（闪电图标与快快启动文字处）
            const double anchorOffsetY = 20; // 标题栏标红区域垂直对齐中心线

            double targetX = cursorX - anchorOffsetX;
            double targetY = cursorY - anchorOffsetY;

            // 显示器工作区防越界修正：若窗口超出右边界或下边界，则向内靠齐，确保窗口完整可见
            if (targetX + targetW > workRight) targetX = Math.Max(workLeft, workRight - targetW);
            if (targetX < workLeft) targetX = workLeft;
            if (targetY + targetH > workBottom) targetY = Math.Max(workTop, workBottom - targetH);
            if (targetY < workTop) targetY = workTop;

            Left = targetX;
            Top = targetY;

            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            ApplyTopmostState(_viewModel.Settings.AlwaysOnTop);
            SaveWindowPlacement();
        }

        /// <summary>
        /// 点击标题栏 GitHub 按钮：在默认浏览器中打开项目主页
        /// </summary>
        private void OnGitHubClick(object sender, RoutedEventArgs e)
        {
            AppConstants.OpenGitHub();
        }

        /// <summary>
        /// 鼠标中键全局点击回调：若已显示则隐藏，若未显示则在光标处呼出
        /// </summary>
        private void OnMiddleMouseClicked()
        {
            if (Visibility == Visibility.Visible && !_edgeDockService.IsHidden)
            {
                Hide();
            }
            else
            {
                SummonAtMouseCursor();
            }
        }

        /// <summary>
        /// 标题栏鼠标左键按下：启动原生窗口拖拽
        /// </summary>
        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep && FindVisualParent<System.Windows.Controls.Button>(dep) != null)
            {
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _edgeDockService.BeginDrag();
                try { DragMove(); } catch { }
                finally
                {
                    _edgeDockService.EndDrag();
                    SaveWindowPlacement();
                }
            }
        }

        /// <summary>
        /// 侧边栏空白区域鼠标左键按下：启动原生窗口拖拽
        /// </summary>
        private void OnSidebarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep &&
                (FindVisualParent<System.Windows.Controls.Button>(dep) != null || FindVisualParent<ListBoxItem>(dep) != null))
            {
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                _edgeDockService.BeginDrag();
                try { DragMove(); } catch { }
                finally
                {
                    _edgeDockService.EndDrag();
                    SaveWindowPlacement();
                }
            }
        }

        /// <summary>
        /// 辅助函数：向上查找可视树中的指定类型父控件
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        /// <summary>
        /// 最小化按钮点击：隐藏窗口至系统托盘
        /// </summary>
        private void OnMinimizeToTrayClick(object sender, RoutedEventArgs e) => Hide();

        /// <summary>
        /// 关闭按钮点击：隐藏窗口至系统托盘
        /// </summary>
        private void OnCloseToTrayClick(object sender, RoutedEventArgs e) => Hide();

        /// <summary>
        /// 唤醒并激活主窗口至前台（从隐藏、托盘、停靠或最小化状态恢复）
        /// </summary>
        public void WakeupAndActivate()
        {
            if (Visibility != Visibility.Visible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (_edgeDockService.IsHidden)
            {
                _edgeDockService.SlideOut();
            }

            Activate();

            ApplyTopmostState(true);
            if (!_viewModel.Settings.AlwaysOnTop)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyTopmostState(false);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }

            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(helper.Handle);
                }
            }
            catch { }
        }

        /// <summary>
        /// 执行真实退出流程，解除窗口拦截并关闭
        /// </summary>
        public void ExitApplication()
        {
            _isRealExit = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// 窗口关闭事件拦截：默认拦截为隐藏至托盘，仅在真正退出时释放全部系统资源
        /// </summary>
        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isRealExit)
            {
                e.Cancel = true;
                Hide();
                SaveWindowPlacement();
                return;
            }

            SaveWindowPlacement();
            _hotkeyService.Dispose();
            _mouseHookService.Dispose();
            _edgeDockService.Dispose();
            _trayIconManager.Dispose();
        }

        #region Drag & Drop Handlers
        /// <summary>
        /// 外部文件拖拽悬停窗口上空：展示拷贝视觉反馈
        /// </summary>
        private void OnWindowDragOver(object sender, DragEventArgs e)
        {
            e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text)) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// 外部文件释放至主窗口空白区域：导入至当前分类默认分组
        /// </summary>
        private void OnWindowDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        var filesCopy = (string[])files.Clone();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(filesCopy, null);
                        }));
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    if (e.Data.GetData(DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        string cleanText = text.Trim();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(new[] { cleanText }, null);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnWindowDrop] Drop error: {ex.Message}");
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 外部文件释放至特定分组卡片内部：精准导入至该分组
        /// </summary>
        private void OnGroupDrop(object sender, DragEventArgs e)
        {
            ShortcutGroupViewModel? targetGroup = null;
            if (sender is FrameworkElement elem && elem.DataContext is ShortcutGroupViewModel grp) targetGroup = grp;

            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        var filesCopy = (string[])files.Clone();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(filesCopy, targetGroup);
                        }));
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    if (e.Data.GetData(DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        string cleanText = text.Trim();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(new[] { cleanText }, targetGroup);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnGroupDrop] Drop error: {ex.Message}");
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 外部文件释放至侧边栏特定分类项上：切换分类并导入其首个分组
        /// </summary>
        private void OnCategoryDrop(object sender, DragEventArgs e)
        {
            CategoryViewModel? targetCat = null;
            if (sender is FrameworkElement elem && elem.DataContext is CategoryViewModel cat) targetCat = cat;
            if (targetCat != null) _viewModel.SelectedCategory = targetCat;

            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                    {
                        var filesCopy = (string[])files.Clone();
                        var group = targetCat?.Groups.FirstOrDefault();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(filesCopy, group);
                        }));
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    if (e.Data.GetData(DataFormats.Text) is string text && !string.IsNullOrWhiteSpace(text))
                    {
                        string cleanText = text.Trim();
                        var group = targetCat?.Groups.FirstOrDefault();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _viewModel.HandleDropFiles(new[] { cleanText }, group);
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OnCategoryDrop] Drop error: {ex.Message}");
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 分组卡片标题栏点击：切换折叠/展开
        /// </summary>
        private void OnGroupHeaderClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ShortcutGroupViewModel grp)
            {
                grp.ToggleExpandCommand.Execute(null);
            }
        }
        #endregion
    }
}