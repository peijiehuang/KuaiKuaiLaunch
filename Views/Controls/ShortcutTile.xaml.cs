using System.Windows.Controls;
using System.Windows.Input;
using KuaiKuaiLaunch.ViewModels;

namespace KuaiKuaiLaunch.Views.Controls
{
    /// <summary>
    /// 单个快捷方式磁贴卡片用户控件
    /// </summary>
    public partial class ShortcutTile : System.Windows.Controls.UserControl
    {
        /// <summary>
        /// 构造函数，初始化磁贴组件
        /// </summary>
        public ShortcutTile()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 鼠标左键点击松开时触发快捷方式启动命令
        /// </summary>
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ShortcutItemViewModel vm)
            {
                vm.LaunchCommand.Execute(null);
            }
        }
    }
}
