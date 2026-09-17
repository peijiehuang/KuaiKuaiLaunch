using System;

namespace KuaiKuaiLaunch.Models
{
    /// <summary>
    /// 窗口贴边停靠方位枚举
    /// </summary>
    public enum DockEdge
    {
        /// <summary>
        /// 无停靠
        /// </summary>
        None,

        /// <summary>
        /// 贴靠顶部
        /// </summary>
        Top,

        /// <summary>
        /// 贴靠左侧
        /// </summary>
        Left,

        /// <summary>
        /// 贴靠右侧
        /// </summary>
        Right
    }

    /// <summary>
    /// 图标显示尺寸规格枚举
    /// </summary>
    public enum IconDisplaySize
    {
        /// <summary>
        /// 小图标 (32x32)
        /// </summary>
        Small = 32,

        /// <summary>
        /// 中等图标 (48x48)
        /// </summary>
        Medium = 48,

        /// <summary>
        /// 大图标 (64x64)
        /// </summary>
        Large = 64
    }

    /// <summary>
    /// 应用程序全局配置数据实体
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// 开机是否自动启动
        /// </summary>
        public bool AutoStartWithWindows { get; set; } = false;

        /// <summary>
        /// 是否开启屏幕边缘吸附
        /// </summary>
        public bool EnableEdgeDocking { get; set; } = true;

        /// <summary>
        /// 停靠在边缘时是否自动滑出隐藏
        /// </summary>
        public bool EnableEdgeAutoHide { get; set; } = true;

        /// <summary>
        /// 移出鼠标后的自动隐藏延时（毫秒）
        /// </summary>
        public int AutoHideDelayMs { get; set; } = 300;

        /// <summary>
        /// 全局唤醒热键文字描述，如 "Alt + Q"
        /// </summary>
        public string GlobalHotkeyText { get; set; } = "Alt + Q";

        /// <summary>
        /// 全局热键修饰键掩码
        /// </summary>
        public uint HotkeyModifiers { get; set; } = 0x0001; // MOD_ALT

        /// <summary>
        /// 全局热键虚拟键码
        /// </summary>
        public uint HotkeyVirtualKey { get; set; } = 0x51;   // VK_Q

        /// <summary>
        /// 是否启用鼠标中键（滚轮点击）全局唤醒/隐藏主面板
        /// </summary>
        public bool EnableMiddleClickWakeup { get; set; } = true;

        /// <summary>
        /// 图标显示大小
        /// </summary>
        public IconDisplaySize IconSize { get; set; } = IconDisplaySize.Medium;

        /// <summary>
        /// 软件主题: "System", "Light", "Dark"
        /// </summary>
        public string Theme { get; set; } = "Dark";

        /// <summary>
        /// 是否窗口置顶
        /// </summary>
        public bool AlwaysOnTop { get; set; } = true;

        /// <summary>
        /// 点击项目启动后是否保持窗口打开（false 则启动后自动隐藏）
        /// </summary>
        public bool KeepOpenAfterLaunch { get; set; } = true;

        /// <summary>
        /// 窗口水平屏幕坐标 X
        /// </summary>
        public double WindowLeft { get; set; } = 200;

        /// <summary>
        /// 窗口垂直屏幕坐标 Y
        /// </summary>
        public double WindowTop { get; set; } = 150;

        /// <summary>
        /// 窗口宽度
        /// </summary>
        public double WindowWidth { get; set; } = 480;

        /// <summary>
        /// 窗口高度
        /// </summary>
        public double WindowHeight { get; set; } = 640;

        /// <summary>
        /// 上次停靠边缘
        /// </summary>
        public DockEdge LastDockEdge { get; set; } = DockEdge.None;
    }
}

