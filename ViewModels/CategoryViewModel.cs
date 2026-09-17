using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 分类视图模型（代表侧边栏中的一个大类抽屉项）
    /// </summary>
    public partial class CategoryViewModel : ObservableObject
    {
        public Category Model { get; }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _iconSymbol;

        public ObservableCollection<ShortcutGroupViewModel> Groups { get; } = new();

        public event Action<CategoryViewModel>? RequestRename;
        public event Action<CategoryViewModel>? RequestDelete;
        public event Action<CategoryViewModel>? RequestAddGroup;
        public event Action<CategoryViewModel>? RequestChangeIcon;

        /// <summary>
        /// 构造函数，基于 Category 模型初始化视图属性
        /// </summary>
        public CategoryViewModel(Category model)
        {
            Model = model;
            _name = model.Name;
            _iconSymbol = string.IsNullOrWhiteSpace(model.IconSymbol) ? "Apps24" : model.IconSymbol;
        }

        partial void OnNameChanged(string value) => Model.Name = value;
        partial void OnIconSymbolChanged(string value) => Model.IconSymbol = value;

        /// <summary>
        /// 请求在此分类下新建子分组
        /// </summary>
        [RelayCommand]
        public void AddGroup() => RequestAddGroup?.Invoke(this);

        /// <summary>
        /// 请求重命名当前分类
        /// </summary>
        [RelayCommand]
        public void Rename() => RequestRename?.Invoke(this);

        /// <summary>
        /// 请求删除当前分类及其所有子项
        /// </summary>
        [RelayCommand]
        public void Delete() => RequestDelete?.Invoke(this);

        /// <summary>
        /// 请求修改当前分类的 Fluent Symbol 图标
        /// </summary>
        [RelayCommand]
        public void ChangeIcon() => RequestChangeIcon?.Invoke(this);
    }
}
