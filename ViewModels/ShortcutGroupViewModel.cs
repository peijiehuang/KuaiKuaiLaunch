using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 快捷项分组视图模型（代表分类下的一个可折叠卡片分组）
    /// </summary>
    public partial class ShortcutGroupViewModel : ObservableObject
    {
        public ShortcutGroup Model { get; }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private bool _isExpanded;

        public ObservableCollection<ShortcutItemViewModel> Items { get; } = new();

        public event Action<ShortcutGroupViewModel>? RequestRename;
        public event Action<ShortcutGroupViewModel>? RequestDelete;
        public event Action<ShortcutGroupViewModel>? RequestAddItem;

        /// <summary>
        /// 构造函数，基于 ShortcutGroup 模型初始化
        /// </summary>
        public ShortcutGroupViewModel(ShortcutGroup model)
        {
            Model = model;
            _name = model.Name;
            _isExpanded = model.IsExpanded;
        }

        partial void OnIsExpandedChanged(bool value) => Model.IsExpanded = value;
        partial void OnNameChanged(string value) => Model.Name = value;

        /// <summary>
        /// 切换当前分组的折叠与展开状态
        /// </summary>
        [RelayCommand]
        public void ToggleExpand() => IsExpanded = !IsExpanded;

        /// <summary>
        /// 请求重命名当前分组
        /// </summary>
        [RelayCommand]
        public void Rename() => RequestRename?.Invoke(this);

        /// <summary>
        /// 请求删除当前分组及其全部快捷项
        /// </summary>
        [RelayCommand]
        public void Delete() => RequestDelete?.Invoke(this);

        /// <summary>
        /// 请求在当前分组内手动新增快捷项目
        /// </summary>
        [RelayCommand]
        public void AddItem() => RequestAddItem?.Invoke(this);
    }
}
