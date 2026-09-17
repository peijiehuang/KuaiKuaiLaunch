using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 单个快捷项视图模型（代表面板中的一个快捷图标磁贴，包含点击启动、管理员运行、图标渲染与右键菜单）
    /// </summary>
    public partial class ShortcutItemViewModel : ObservableObject
    {
        private readonly LauncherService _launcherService;
        private readonly IconExtractorService _iconExtractorService;

        public ShortcutItem Model { get; }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _targetPath;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private bool _runAsAdmin;

        [ObservableProperty]
        private ImageSource? _iconImage;

        public event Action<ShortcutItemViewModel>? RequestEdit;
        public event Action<ShortcutItemViewModel>? RequestDelete;
        public event Action? ItemLaunched;

        /// <summary>
        /// 构造函数，基于模型及执行器/图标提取器服务初始化
        /// </summary>
        public ShortcutItemViewModel(ShortcutItem model, LauncherService launcherService, IconExtractorService iconExtractorService)
        {
            Model = model;
            _launcherService = launcherService;
            _iconExtractorService = iconExtractorService;

            _name = model.Name;
            _targetPath = model.TargetPath;
            _description = model.Description;
            _runAsAdmin = model.RunAsAdmin;
            LoadIcon();
        }

        /// <summary>
        /// 从本地缓存或物理文件中加载图标至 WPF ImageSource
        /// </summary>
        public void LoadIcon() => IconImage = _iconExtractorService.LoadIconImage(Model);

        /// <summary>
        /// 从 Model 最新数据刷新当前 ViewModel 的各项呈现属性
        /// </summary>
        public void RefreshFromModel()
        {
            Name = Model.Name;
            TargetPath = Model.TargetPath;
            Description = Model.Description;
            RunAsAdmin = Model.RunAsAdmin;
            LoadIcon();
        }

        /// <summary>
        /// 执行快捷启动操作
        /// </summary>
        [RelayCommand]
        public void Launch()
        {
            if (_launcherService.Launch(Model, out var error))
            {
                ItemLaunched?.Invoke();
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(error, "启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 以管理员身份提权启动当前项目
        /// </summary>
        [RelayCommand]
        public void LaunchAsAdmin()
        {
            bool original = Model.RunAsAdmin;
            Model.RunAsAdmin = true;
            Launch();
            Model.RunAsAdmin = original;
        }

        /// <summary>
        /// 在 Windows 资源管理器中定位当前项目所在位置
        /// </summary>
        [RelayCommand]
        public void OpenFileLocation() => _launcherService.OpenFileLocation(Model);

        /// <summary>
        /// 弹出编辑对话框修改当前项目属性
        /// </summary>
        [RelayCommand]
        public void Edit() => RequestEdit?.Invoke(this);

        /// <summary>
        /// 请求删除当前快捷项目
        /// </summary>
        [RelayCommand]
        public void Delete() => RequestDelete?.Invoke(this);
    }
}
