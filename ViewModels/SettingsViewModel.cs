using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 主题选项实体
    /// </summary>
    public class ThemeOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// 图标尺寸选项实体
    /// </summary>
    public class IconSizeOption
    {
        public string DisplayName { get; set; } = string.Empty;
        public IconDisplaySize Value { get; set; }
    }

    /// <summary>
    /// 全局设置对话框 ViewModel（管理开机自启、边缘吸附、自动隐藏延时、中键唤醒、置顶与主题等）
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly AutoStartService _autoStartService;

        [ObservableProperty]
        private bool _autoStartWithWindows;

        [ObservableProperty]
        private bool _checkDesktopShortcutOnStartup;

        [ObservableProperty]
        private bool _enableEdgeDocking;

        [ObservableProperty]
        private bool _enableEdgeAutoHide;

        [ObservableProperty]
        private int _autoHideDelayMs;

        [ObservableProperty]
        private string _globalHotkeyText;

        [ObservableProperty]
        private bool _enableMiddleClickWakeup;

        [ObservableProperty]
        private bool _alwaysOnTop;

        [ObservableProperty]
        private bool _keepOpenAfterLaunch;

        [ObservableProperty]
        private ThemeOption _selectedTheme;

        [ObservableProperty]
        private IconSizeOption _selectedIconSize;

        public List<ThemeOption> AvailableThemes { get; } = new()
        {
            new() { DisplayName = "深色模式 (Dark)", Value = "Dark" },
            new() { DisplayName = "浅色模式 (Light)", Value = "Light" },
            new() { DisplayName = "跟随系统 (System)", Value = "System" }
        };

        public List<IconSizeOption> AvailableIconSizes { get; } = new()
        {
            new() { DisplayName = "小图标 (32px)", Value = IconDisplaySize.Small },
            new() { DisplayName = "中等图标 (48px - 推荐)", Value = IconDisplaySize.Medium },
            new() { DisplayName = "大图标 (64px)", Value = IconDisplaySize.Large }
        };

        public bool DialogResult { get; private set; } = false;
        public event Action? RequestClose;

        /// <summary>
        /// 构造函数，加载当前软件设置与开机自启动注册表状态
        /// </summary>
        public SettingsViewModel(AppSettings settings, AutoStartService autoStartService)
        {
            _settings = settings;
            _autoStartService = autoStartService;

            _autoStartWithWindows = _autoStartService.IsAutoStartEnabled();
            _checkDesktopShortcutOnStartup = settings.CheckDesktopShortcutOnStartup;
            _enableEdgeDocking = settings.EnableEdgeDocking;
            _enableEdgeAutoHide = settings.EnableEdgeAutoHide;
            _autoHideDelayMs = settings.AutoHideDelayMs;
            _globalHotkeyText = settings.GlobalHotkeyText;
            _enableMiddleClickWakeup = settings.EnableMiddleClickWakeup;
            _alwaysOnTop = settings.AlwaysOnTop;
            _keepOpenAfterLaunch = settings.KeepOpenAfterLaunch;

            _selectedTheme = AvailableThemes.Find(t => t.Value == settings.Theme) ?? AvailableThemes[0];
            _selectedIconSize = AvailableIconSizes.Find(s => s.Value == settings.IconSize) ?? AvailableIconSizes[1];
        }

        /// <summary>
        /// 保存设置至 AppSettings 并更新注册表开机自启
        /// </summary>
        [RelayCommand]
        public void Save()
        {
            _settings.AutoStartWithWindows = AutoStartWithWindows;
            _autoStartService.SetAutoStart(AutoStartWithWindows);
            _settings.CheckDesktopShortcutOnStartup = CheckDesktopShortcutOnStartup;

            _settings.EnableEdgeDocking = EnableEdgeDocking;
            _settings.EnableEdgeAutoHide = EnableEdgeAutoHide;
            _settings.AutoHideDelayMs = AutoHideDelayMs;
            _settings.GlobalHotkeyText = GlobalHotkeyText;
            _settings.EnableMiddleClickWakeup = EnableMiddleClickWakeup;
            _settings.AlwaysOnTop = AlwaysOnTop;
            _settings.KeepOpenAfterLaunch = KeepOpenAfterLaunch;
            _settings.Theme = SelectedTheme.Value;
            _settings.IconSize = SelectedIconSize.Value;

            DialogResult = true;
            RequestClose?.Invoke();
        }

        /// <summary>
        /// 取消保存并关闭设置对话框
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke();
        }

        /// <summary>
        /// 当前软件版本号
        /// </summary>
        public string AppVersion => AppConstants.AppVersion;

        /// <summary>
        /// GitHub 项目地址
        /// </summary>
        public string GitHubUrl => AppConstants.GitHubUrl;

        /// <summary>
        /// 在默认浏览器中打开 GitHub 项目仓库主页
        /// </summary>
        [RelayCommand]
        public void OpenGitHub()
        {
            AppConstants.OpenGitHub();
        }
    }
}
