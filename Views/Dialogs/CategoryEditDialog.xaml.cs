using Wpf.Ui.Controls;
using KuaiKuaiLaunch.ViewModels;

namespace KuaiKuaiLaunch.Views.Dialogs
{
    /// <summary>
    /// 分类编辑与新建对话框代码隐藏
    /// </summary>
    public partial class CategoryEditDialog : FluentWindow
    {
        /// <summary>
        /// 构造函数，绑定 ViewModel 并挂载关闭事件
        /// </summary>
        /// <param name="viewModel">分类编辑视图模型</param>
        public CategoryEditDialog(CategoryEditViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
            InitializeComponent();
        }
    }
}
