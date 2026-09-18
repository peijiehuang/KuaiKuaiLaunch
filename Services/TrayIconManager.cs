using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KuaiKuaiLaunch.ViewModels;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 系统托盘常驻图标与上下文右键菜单管理器
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly MainWindow _mainWindow;
        private readonly MainViewModel _mainViewModel;
        private readonly EdgeDockService _edgeDockService;

        /// <summary>
        /// 构造函数，初始化托盘图标与托盘右键菜单
        /// </summary>
        public TrayIconManager(MainWindow mainWindow, MainViewModel mainViewModel, EdgeDockService edgeDockService)
        {
            _mainWindow = mainWindow;
            _mainViewModel = mainViewModel;
            _edgeDockService = edgeDockService;

            _notifyIcon = new NotifyIcon { Text = $"{AppConstants.AppName} {AppConstants.AppVersion}", Visible = true };
            LoadTrayIcon();

            var menu = new ContextMenuStrip();
            var showItem = new ToolStripMenuItem("显示主面板", null, (s, e) => ShowMainWindow());
            showItem.Font = new Font(showItem.Font, FontStyle.Bold);
            menu.Items.Add(showItem);

            var topItem = new ToolStripMenuItem("窗口置顶", null, (s, e) => _mainViewModel.ToggleAlwaysOnTopCommand.Execute(null));
            menu.Items.Add(topItem);
            menu.Items.Add(new ToolStripMenuItem("新建分类", null, (s, e) => _mainViewModel.AddCategoryCommand.Execute(null)));
            menu.Items.Add(new ToolStripMenuItem("软件设置", null, (s, e) => _mainViewModel.OpenSettingsCommand.Execute(null)));
            menu.Items.Add(new ToolStripMenuItem("GitHub 项目主页", null, (s, e) => AppConstants.OpenGitHub()));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("退出快快启动", null, (s, e) => ExitApp()));

            menu.Opening += (s, e) => topItem.Checked = _mainViewModel.IsAlwaysOnTop;
            _notifyIcon.ContextMenuStrip = menu;

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) _edgeDockService.ToggleShowHide();
            };

            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        /// <summary>
        /// 提取当前主程序的关联应用图标作为托盘小图标呈现
        /// </summary>
        private void LoadTrayIcon()
        {
            try
            {
                var iconUri = new Uri("pack://application:,,,/assets/app.ico");
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo?.Stream != null)
                {
                    using var stream = streamInfo.Stream;
                    _notifyIcon.Icon = new Icon(stream, 32, 32);
                    return;
                }
            }
            catch { }

            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        _notifyIcon.Icon = icon;
                        return;
                    }
                }
            }
            catch { }

            _notifyIcon.Icon = SystemIcons.Application;
        }

        /// <summary>
        /// 激活并呼出主窗口（解除最小化并滑出边缘停靠）
        /// </summary>
        public void ShowMainWindow()
        {
            _mainWindow.WakeupAndActivate();
        }

        /// <summary>
        /// 安全退出应用程序并清理托盘图标
        /// </summary>
        public void ExitApp()
        {
            _notifyIcon.Visible = false;
            _mainWindow.ExitApplication();
        }

        /// <summary>
        /// 释放托盘图标资源
        /// </summary>
        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
