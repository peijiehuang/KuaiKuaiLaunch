using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 快捷方式属性编辑与新增对话框 ViewModel
    /// </summary>
    public partial class ItemEditViewModel : ObservableObject
    {
        private readonly StorageService _storageService;
        private readonly IconExtractorService _iconExtractorService;

        public ShortcutItem Item { get; }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _targetPath;

        [ObservableProperty]
        private string _arguments;

        [ObservableProperty]
        private string _workingDirectory;

        [ObservableProperty]
        private bool _runAsAdmin;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private string _customIconPath;

        [ObservableProperty]
        private ImageSource? _iconPreview;

        public bool DialogResult { get; private set; } = false;
        public event Action? RequestClose;

        /// <summary>
        /// 构造函数，基于 ShortcutItem 模型初始化编辑字段及实时图标预览
        /// </summary>
        public ItemEditViewModel(ShortcutItem item, bool isNew, StorageService storageService, IconExtractorService iconExtractorService)
        {
            Item = item;
            _storageService = storageService;
            _iconExtractorService = iconExtractorService;

            Title = isNew ? "添加快捷方式" : "快捷方式属性";
            _name = item.Name;
            _targetPath = item.TargetPath;
            _arguments = item.Arguments;
            _workingDirectory = item.WorkingDirectory;
            _runAsAdmin = item.RunAsAdmin;
            _description = item.Description;
            _customIconPath = item.CustomIconPath;

            UpdateIconPreview();
        }

        /// <summary>
        /// 浏览选择本地程序或文件，并自动推导项目名称及工作目录
        /// </summary>
        [RelayCommand]
        public void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择目标文件或程序",
                Filter = "常用程序与文件|*.exe;*.lnk;*.bat;*.cmd;*.url;*.*|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                TargetPath = _storageService.ToPortablePath(dialog.FileName);
                if (string.IsNullOrWhiteSpace(Name)) Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(WorkingDirectory))
                {
                    string? dir = Path.GetDirectoryName(dialog.FileName);
                    if (!string.IsNullOrEmpty(dir)) WorkingDirectory = _storageService.ToPortablePath(dir);
                }
                UpdateIconPreview();
            }
        }

        /// <summary>
        /// 浏览选择目标文件夹
        /// </summary>
        [RelayCommand]
        public void BrowseFolder()
        {
            var dialog = new OpenFolderDialog { Title = "选择目标文件夹" };
            if (dialog.ShowDialog() == true)
            {
                TargetPath = _storageService.ToPortablePath(dialog.FolderName);
                if (string.IsNullOrWhiteSpace(Name)) Name = Path.GetFileName(dialog.FolderName);
                UpdateIconPreview();
            }
        }

        /// <summary>
        /// 浏览选择程序工作起始目录
        /// </summary>
        [RelayCommand]
        public void BrowseWorkDir()
        {
            var dialog = new OpenFolderDialog { Title = "选择工作目录" };
            if (dialog.ShowDialog() == true)
            {
                WorkingDirectory = _storageService.ToPortablePath(dialog.FolderName);
            }
        }

        /// <summary>
        /// 浏览选择自定义图标文件（.ico, .png, .exe, .dll）
        /// </summary>
        [RelayCommand]
        public void BrowseIcon()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择图标文件",
                Filter = "图标与可执行文件|*.ico;*.png;*.exe;*.dll|所有文件|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                CustomIconPath = _storageService.ToPortablePath(dialog.FileName);
                UpdateIconPreview();
            }
        }

        /// <summary>
        /// 根据当前目标路径或自定义图标更新弹窗内的图标实时预览
        /// </summary>
        private void UpdateIconPreview()
        {
            var tempItem = new ShortcutItem
            {
                Id = Item.Id,
                TargetPath = TargetPath,
                CustomIconPath = CustomIconPath,
                ItemType = Item.ItemType
            };

            _iconExtractorService.ExtractAndSaveIcon(tempItem);
            IconPreview = _iconExtractorService.LoadIconImage(tempItem);
        }

        /// <summary>
        /// 校验表单并将更改写回 ShortcutItem 模型，关闭弹窗
        /// </summary>
        [RelayCommand]
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Item.Name = Name;
            Item.TargetPath = TargetPath;
            Item.Arguments = Arguments;
            Item.WorkingDirectory = WorkingDirectory;
            Item.RunAsAdmin = RunAsAdmin;
            Item.Description = Description;
            Item.CustomIconPath = CustomIconPath;

            _iconExtractorService.ExtractAndSaveIcon(Item);
            DialogResult = true;
            RequestClose?.Invoke();
        }

        /// <summary>
        /// 取消编辑并关闭弹窗
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke();
        }
    }
}
