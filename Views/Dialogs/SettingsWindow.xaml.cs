using Wpf.Ui.Controls;
using KuaiKuaiLaunch.ViewModels;

namespace KuaiKuaiLaunch.Views.Dialogs
{
    /// <summary>
    /// 系统设置对话框窗口
    /// </summary>
    public partial class SettingsWindow : FluentWindow
    {
        /// <summary>
        /// 初始化系统设置对话框窗口实例
        /// </summary>
        /// <param name="viewModel">设置视图模型</param>
        public SettingsWindow(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
            InitializeComponent();
        }
    }
}
