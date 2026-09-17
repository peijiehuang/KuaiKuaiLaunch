using System;

namespace KuaiKuaiLaunch.Models
{
    /// <summary>
    /// 快捷启动项目标类型
    /// </summary>
    public enum ShortcutType
    {
        /// <summary>
        /// 可执行文件 (.exe, .bat, .cmd 等)
        /// </summary>
        Application,

        /// <summary>
        /// 普通文件文档
        /// </summary>
        File,

        /// <summary>
        /// 文件夹目录
        /// </summary>
        Folder,

        /// <summary>
        /// 网页超链接
        /// </summary>
        Url,

        /// <summary>
        /// 自定义命令行指令
        /// </summary>
        CustomCommand
    }

    /// <summary>
    /// 快捷启动项目数据实体
    /// </summary>
    public class ShortcutItem
    {
        /// <summary>
        /// 唯一标识 GUID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 目标路径（支持相对路径如 "..\tools\app.exe"、网址或环境变量 "%APP_DIR%\..."）
        /// </summary>
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// 命令行参数
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// 工作目录
        /// </summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 是否以管理员权限运行
        /// </summary>
        public bool RunAsAdmin { get; set; } = false;

        /// <summary>
        /// 快捷方式类型
        /// </summary>
        public ShortcutType ItemType { get; set; } = ShortcutType.Application;

        /// <summary>
        /// 缓存的高清图标本地相对路径（如 "icons/{id}.png"）
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// 自定义外部图标路径（若用户主动指定）
        /// </summary>
        public string CustomIconPath { get; set; } = string.Empty;

        /// <summary>
        /// 备注 / 提示文字
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 启动热键（如 "Ctrl+Alt+1"）
        /// </summary>
        public string Hotkey { get; set; } = string.Empty;

        /// <summary>
        /// 启动次数统计
        /// </summary>
        public int LaunchCount { get; set; } = 0;

        /// <summary>
        /// 最近启动时间
        /// </summary>
        public DateTime? LastLaunchTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 排序索引
        /// </summary>
        public int OrderIndex { get; set; } = 0;
    }
}

