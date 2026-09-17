using Wpf.Ui.Controls;
using KuaiKuaiLaunch.ViewModels;

namespace KuaiKuaiLaunch.Views.Dialogs
{
    /// <summary>
    /// 快捷方式添加与属性编辑对话框代码隐藏
    /// </summary>
    public partial class ItemEditDialog : FluentWindow
    {
        /// <summary>
        /// 构造函数，绑定 ViewModel 并挂载关闭事件
        /// </summary>
        /// <param name="viewModel">快捷项编辑视图模型</param>
        public ItemEditDialog(ItemEditViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
            InitializeComponent();
        }
    }
}
