using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;

namespace KuaiKuaiLaunch.ViewModels
{
    /// <summary>
    /// 快快启动主界面 ViewModel（主控状态、分类管理、分组管理、文件拖拽导入与设置）
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        public StorageService StorageService { get; }
        public LnkParserService LnkParserService { get; }
        public IconExtractorService IconExtractorService { get; }
        public LauncherService LauncherService { get; }
        public AutoStartService AutoStartService { get; }

        public AppSettings Settings { get; }
        public ObservableCollection<CategoryViewModel> Categories { get; } = new();

        [ObservableProperty]
        private CategoryViewModel? _selectedCategory;

        [ObservableProperty]
        private bool _isAlwaysOnTop = true;

        public bool IsPinned
        {
            get => IsAlwaysOnTop;
            set => IsAlwaysOnTop = value;
        }

        [ObservableProperty]
        private int _iconPixelSize = 48;

        public event Action<ShortcutItem, bool, Action<bool>>? RequestShowItemEditDialog;
        public event Action<Category, bool, Action<bool>>? RequestShowCategoryEditDialog;
        public event Action<AppSettings, Action<bool>>? RequestShowSettingsDialog;
        public event Action? RequestSlideInOrHide;
        public event Action<bool>? RequestUpdateTopmost;

        /// <summary>
        /// 构造函数，依赖注入各项核心服务并加载用户配置及分类数据
        /// </summary>
        public MainViewModel(StorageService storageService, LnkParserService lnkParserService, IconExtractorService iconExtractorService, LauncherService launcherService, AutoStartService autoStartService)
        {
            StorageService = storageService;
            LnkParserService = lnkParserService;
            IconExtractorService = iconExtractorService;
            LauncherService = launcherService;
            AutoStartService = autoStartService;

            Settings = StorageService.LoadSettings();
            IsAlwaysOnTop = Settings.AlwaysOnTop;
            UpdateIconSize();
            LoadCategories();
        }

        /// <summary>
        /// 根据配置更新图标显示像素尺寸
        /// </summary>
        public void UpdateIconSize() => IconPixelSize = (int)Settings.IconSize;

        /// <summary>
        /// 从持久化存储中读取并构建完整的 CategoryViewModel 视图模型层级树
        /// </summary>
        public void LoadCategories()
        {
            Categories.Clear();
            var rawCategories = StorageService.LoadCategories();
            foreach (var cat in rawCategories)
            {
                Categories.Add(CreateCategoryViewModel(cat));
            }
            SelectedCategory = Categories.FirstOrDefault();
        }

        /// <summary>
        /// 将当前所有分类、分组及快捷方式的数据全量同步回模型并执行安全落盘
        /// </summary>
        public void SaveCategories()
        {
            var rawList = new List<Category>();
            for (int i = 0; i < Categories.Count; i++)
            {
                var catVm = Categories[i];
                catVm.Model.OrderIndex = i;
                catVm.Model.Groups.Clear();

                for (int j = 0; j < catVm.Groups.Count; j++)
                {
                    var grpVm = catVm.Groups[j];
                    grpVm.Model.OrderIndex = j;
                    grpVm.Model.Items.Clear();

                    for (int k = 0; k < grpVm.Items.Count; k++)
                    {
                        var itmVm = grpVm.Items[k];
                        itmVm.Model.OrderIndex = k;
                        grpVm.Model.Items.Add(itmVm.Model);
                    }

                    catVm.Model.Groups.Add(grpVm.Model);
                }

                rawList.Add(catVm.Model);
            }

            StorageService.SaveCategories(rawList);
        }

        /// <summary>
        /// 创建并配置单个分类的 ViewModel 及其事件管道
        /// </summary>
        private CategoryViewModel CreateCategoryViewModel(Category cat)
        {
            var catVm = new CategoryViewModel(cat);
            catVm.RequestAddGroup += OnRequestAddGroup;
            catVm.RequestRename += OnRequestRenameCategory;
            catVm.RequestDelete += OnRequestDeleteCategory;
            catVm.RequestChangeIcon += OnRequestRenameCategory;

            foreach (var grp in cat.Groups)
            {
                catVm.Groups.Add(CreateGroupViewModel(grp));
            }
            return catVm;
        }

        /// <summary>
        /// 创建并配置单个分组的 ViewModel 及其事件管道
        /// </summary>
        private ShortcutGroupViewModel CreateGroupViewModel(ShortcutGroup grp)
        {
            var grpVm = new ShortcutGroupViewModel(grp);
            grpVm.RequestRename += OnRequestRenameGroup;
            grpVm.RequestDelete += OnRequestDeleteGroup;
            grpVm.RequestAddItem += OnRequestAddItem;

            foreach (var item in grp.Items)
            {
                grpVm.Items.Add(CreateItemViewModel(item));
            }
            return grpVm;
        }

        /// <summary>
        /// 创建并配置单个快捷项的 ViewModel 及其点击启动监听
        /// </summary>
        private ShortcutItemViewModel CreateItemViewModel(ShortcutItem item)
        {
            var itemVm = new ShortcutItemViewModel(item, LauncherService, IconExtractorService);
            itemVm.RequestEdit += OnRequestEditItem;
            itemVm.RequestDelete += OnRequestDeleteItem;
            itemVm.ItemLaunched += OnItemLaunched;
            return itemVm;
        }

        /// <summary>
        /// 项目成功启动回调：保存启动计数并根据设置决定是否自动滑入隐藏
        /// </summary>
        private void OnItemLaunched()
        {
            SaveCategories();
            if (!Settings.KeepOpenAfterLaunch) RequestSlideInOrHide?.Invoke();
        }

        #region Drag & Drop Processing
        /// <summary>
        /// 处理外部拖拽批量导入文件（深度解析快捷方式、抓取并缓存图标、建立数据关联）
        /// </summary>
        /// <param name="paths">拖拽的文件物理绝对路径列表</param>
        /// <param name="targetGroup">目标放置分组（为空时默认放至当前分类第一个分组）</param>
        public void HandleDropFiles(string[] paths, ShortcutGroupViewModel? targetGroup)
        {
            if (paths == null || paths.Length == 0) return;

            if (targetGroup == null)
            {
                if (SelectedCategory == null)
                {
                    if (Categories.Count == 0) AddCategory();
                    SelectedCategory = Categories[0];
                }

                if (SelectedCategory.Groups.Count == 0)
                {
                    var newGrp = new ShortcutGroup { Name = "快捷应用" };
                    var newGrpVm = CreateGroupViewModel(newGrp);
                    SelectedCategory.Groups.Add(newGrpVm);
                    targetGroup = newGrpVm;
                }
                else
                {
                    targetGroup = SelectedCategory.Groups[0];
                }
            }

            foreach (var path in paths)
            {
                try
                {
                    var shortcutItem = LnkParserService.ParseDroppedPath(path);
                    IconExtractorService.ExtractAndSaveIcon(shortcutItem, path);
                    targetGroup.Items.Add(CreateItemViewModel(shortcutItem));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] HandleDrop error: {ex.Message}");
                }
            }

            SaveCategories();
        }
        #endregion

        #region Category Actions
        /// <summary>
        /// 呼出新增分类对话框并完成分类创建
        /// </summary>
        [RelayCommand]
        public void AddCategory()
        {
            var newCat = new Category { Name = "新分类" };
            RequestShowCategoryEditDialog?.Invoke(newCat, true, success =>
            {
                if (success)
                {
                    newCat.Groups.Add(new ShortcutGroup { Name = "默认分组" });
                    var catVm = CreateCategoryViewModel(newCat);
                    Categories.Add(catVm);
                    SelectedCategory = catVm;
                    SaveCategories();
                }
            });
        }

        /// <summary>
        /// 呼出重命名/更换图标对话框并更新分类
        /// </summary>
        private void OnRequestRenameCategory(CategoryViewModel catVm)
        {
            RequestShowCategoryEditDialog?.Invoke(catVm.Model, false, success =>
            {
                if (success)
                {
                    catVm.Name = catVm.Model.Name;
                    catVm.IconSymbol = catVm.Model.IconSymbol;
                    SaveCategories();
                }
            });
        }

        /// <summary>
        /// 确认并删除指定分类及其包含的所有分组与快捷方式
        /// </summary>
        private void OnRequestDeleteCategory(CategoryViewModel catVm)
        {
            var result = MessageBox.Show($"确定要删除分类“{catVm.Name}”及其下的所有内容吗？", "删除分类确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                int index = Categories.IndexOf(catVm);
                Categories.Remove(catVm);
                if (SelectedCategory == catVm)
                {
                    SelectedCategory = Categories.ElementAtOrDefault(Math.Max(0, index - 1));
                }
                SaveCategories();
            }
        }
        #endregion

        #region Group Actions
        /// <summary>
        /// 在指定分类下追加一个新的快捷分组
        /// </summary>
        private void OnRequestAddGroup(CategoryViewModel catVm)
        {
            var newGrp = new ShortcutGroup { Name = $"分组 {catVm.Groups.Count + 1}" };
            catVm.Groups.Add(CreateGroupViewModel(newGrp));
            SaveCategories();
        }

        /// <summary>
        /// 分组重命名请求
        /// </summary>
        private void OnRequestRenameGroup(ShortcutGroupViewModel grpVm)
        {
            SaveCategories();
        }

        /// <summary>
        /// 确认并删除指定分组及其全部快捷项
        /// </summary>
        private void OnRequestDeleteGroup(ShortcutGroupViewModel grpVm)
        {
            if (SelectedCategory == null) return;
            var result = MessageBox.Show($"确定要删除分组“{grpVm.Name}”及其所有图标吗？", "删除分组确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                SelectedCategory.Groups.Remove(grpVm);
                SaveCategories();
            }
        }
        #endregion

        #region Item Actions
        /// <summary>
        /// 在指定分组中弹出项目编辑框并新增快捷方式
        /// </summary>
        private void OnRequestAddItem(ShortcutGroupViewModel grpVm)
        {
            var newItem = new ShortcutItem { Name = "新建项目" };
            RequestShowItemEditDialog?.Invoke(newItem, true, success =>
            {
                if (success)
                {
                    grpVm.Items.Add(CreateItemViewModel(newItem));
                    SaveCategories();
                }
            });
        }

        /// <summary>
        /// 弹出编辑对话框并修改选中的快捷方式属性
        /// </summary>
        private void OnRequestEditItem(ShortcutItemViewModel itemVm)
        {
            RequestShowItemEditDialog?.Invoke(itemVm.Model, false, success =>
            {
                if (success)
                {
                    itemVm.RefreshFromModel();
                    SaveCategories();
                }
            });
        }

        /// <summary>
        /// 确认并删除指定的快捷方式
        /// </summary>
        private void OnRequestDeleteItem(ShortcutItemViewModel itemVm)
        {
            var result = MessageBox.Show($"确定要删除快捷方式“{itemVm.Name}”吗？", "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                if (SelectedCategory != null)
                {
                    foreach (var grp in SelectedCategory.Groups)
                    {
                        if (grp.Items.Remove(itemVm)) break;
                    }
                }
                SaveCategories();
            }
        }
        #endregion

        #region Global Commands
        /// <summary>
        /// 打开全局设置弹窗并同步持久化
        /// </summary>
        [RelayCommand]
        public void OpenSettings()
        {
            RequestShowSettingsDialog?.Invoke(Settings, success =>
            {
                if (success)
                {
                    StorageService.SaveSettings(Settings);
                    UpdateIconSize();
                }
            });
        }

        /// <summary>
        /// 切换窗口总在最前（置顶）状态并保存
        /// </summary>
        [RelayCommand]
        public void ToggleAlwaysOnTop()
        {
            IsAlwaysOnTop = !IsAlwaysOnTop;
            Settings.AlwaysOnTop = IsAlwaysOnTop;
            StorageService.SaveSettings(Settings);
            RequestUpdateTopmost?.Invoke(IsAlwaysOnTop);
        }

        /// <summary>
        /// 切换置顶图钉别名命令
        /// </summary>
        [RelayCommand]
        public void TogglePin() => ToggleAlwaysOnTop();
        #endregion
    }
}
