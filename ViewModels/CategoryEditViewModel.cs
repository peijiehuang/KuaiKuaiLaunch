using System;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 分类图标选项实体
    /// </summary>
    public class SymbolOption
    {
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
    }

    /// <summary>
    /// 分类编辑与新建对话框 ViewModel
    /// </summary>
    public partial class CategoryEditViewModel : ObservableObject
    {
        public Category Category { get; }

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private SymbolOption _selectedSymbol;

        public List<SymbolOption> AvailableSymbols { get; } = new()
        {
            new() { Name = "应用中心", Symbol = "Apps24" },
            new() { Name = "常用工具", Symbol = "Wrench24" },
            new() { Name = "办公商务", Symbol = "Briefcase24" },
            new() { Name = "网络浏览", Symbol = "Globe24" },
            new() { Name = "文档资料", Symbol = "Document24" },
            new() { Name = "文件夹", Symbol = "Folder24" },
            new() { Name = "编程开发", Symbol = "Code24" },
            new() { Name = "游戏娱乐", Symbol = "Games24" },
            new() { Name = "影音视听", Symbol = "MusicNote224" },
            new() { Name = "视频播放", Symbol = "Video24" },
            new() { Name = "系统控制", Symbol = "Settings24" },
            new() { Name = "星标收藏", Symbol = "Star24" },
            new() { Name = "云端服务", Symbol = "Cloud24" },
            new() { Name = "桌面图标", Symbol = "Desktop24" }
        };

        public bool DialogResult { get; private set; } = false;
        public event Action? RequestClose;

        /// <summary>
        /// 构造函数，基于 Category 模型初始化编辑字段及可选图标
        /// </summary>
        public CategoryEditViewModel(Category category, bool isNew)
        {
            Category = category;
            Title = isNew ? "新建分类" : "编辑分类";
            _name = category.Name;
            var match = AvailableSymbols.Find(s => s.Symbol == category.IconSymbol);
            _selectedSymbol = match ?? AvailableSymbols[0];
        }

        /// <summary>
        /// 保存更改并写回模型，关闭对话框
        /// </summary>
        [RelayCommand]
        public void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("分类名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Category.Name = Name;
            Category.IconSymbol = SelectedSymbol.Symbol;
            DialogResult = true;
            RequestClose?.Invoke();
        }

        /// <summary>
        /// 取消编辑并关闭对话框
        /// </summary>
        [RelayCommand]
        public void Cancel()
        {
            DialogResult = false;
            RequestClose?.Invoke();
        }
    }
}
