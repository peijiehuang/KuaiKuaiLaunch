using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using KuaiKuaiLaunch.Models;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 本地便携存储与路径转换服务（负责分类、分组、快捷方式与设置的 JSON 持久化及相对路径解析）
    /// </summary>
    public class StorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        public string BaseDirectory { get; }
        public string DataDirectory { get; }
        public string IconsDirectory { get; }
        public string ShortcutsFilePath { get; }
        public string SettingsFilePath { get; }

        /// <summary>
        /// 构造函数，初始化便携数据目录及文件路径
        /// </summary>
        public StorageService()
        {
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DataDirectory = Path.Combine(BaseDirectory, "data");
            IconsDirectory = Path.Combine(DataDirectory, "icons");
            ShortcutsFilePath = Path.Combine(DataDirectory, "shortcuts.json");
            SettingsFilePath = Path.Combine(DataDirectory, "settings.json");
            EnsureDirectoriesExist();
        }

        /// <summary>
        /// 确保数据存储与图标缓存目录存在
        /// </summary>
        public void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(DataDirectory)) Directory.CreateDirectory(DataDirectory);
            if (!Directory.Exists(IconsDirectory)) Directory.CreateDirectory(IconsDirectory);
        }

        #region Path Resolution & Relative Conversion
        /// <summary>
        /// 将路径解析为可执行物理路径（支持 URL 直通、%APP_DIR% 便携宏、环境变量展开、内置系统命令及相对路径）
        /// </summary>
        /// <param name="path">原始路径或网址</param>
        /// <returns>解析后的绝对路径或原网址/命令</returns>
        public string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string trimmed = path.Trim();

            // 1. Web 网址或自定义协议直通
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !uri.IsFile))
            {
                return trimmed;
            }

            // 2. 展开环境变量与 %APP_DIR% 便携宏
            string expanded = Environment.ExpandEnvironmentVariables(trimmed);
            expanded = expanded.Replace("%APP_DIR%", BaseDirectory.TrimEnd('\\', '/'));

            // 3. 已经是绝对物理路径
            if (Path.IsPathRooted(expanded))
            {
                return Path.GetFullPath(expanded);
            }

            // 4. 存在于程序安装目录内部的相对路径
            string appRelative = Path.Combine(BaseDirectory, expanded);
            if (File.Exists(appRelative) || Directory.Exists(appRelative))
            {
                return Path.GetFullPath(appRelative);
            }

            // 5. Windows 系统内置工具（如 explorer.exe, notepad.exe, calc.exe）
            string sys32Path = Path.Combine(Environment.SystemDirectory, expanded);
            if (File.Exists(sys32Path)) return sys32Path;

            string winPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), expanded);
            if (File.Exists(winPath)) return winPath;

            // 6. 带有目录分隔符的路径，默认为程序相对路径
            if (expanded.Contains('\\') || expanded.Contains('/'))
            {
                return Path.GetFullPath(appRelative);
            }

            // 7. 纯指令名称（如 git.exe, code, wt.exe），交由操作系统 PATH 环境检索
            return expanded;
        }

        /// <summary>
        /// 将绝对物理路径转换为带有 %APP_DIR% 的便携相对路径（如属于程序根目录下）
        /// </summary>
        /// <param name="fullPath">物理绝对路径</param>
        /// <returns>便携相对路径或原路径</returns>
        public string ToPortablePath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;
            string trimmed = fullPath.Trim();

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            try
            {
                string normBase = Path.GetFullPath(BaseDirectory).TrimEnd('\\', '/') + "\\";
                string normTarget = Path.GetFullPath(trimmed);
                if (normTarget.StartsWith(normBase, StringComparison.OrdinalIgnoreCase))
                {
                    return "%APP_DIR%\\" + normTarget.Substring(normBase.Length);
                }
            }
            catch { }

            return fullPath;
        }
        #endregion

        #region Categories & Shortcuts Persistence
        /// <summary>
        /// 从 data/shortcuts.json 读取所有主分类、子分组及快捷方式；若文件不存在则初始化默认模板
        /// </summary>
        /// <returns>分类列表</returns>
        public List<Category> LoadCategories()
        {
            try
            {
                if (File.Exists(ShortcutsFilePath))
                {
                    string json = File.ReadAllText(ShortcutsFilePath);
                    var categories = JsonSerializer.Deserialize<List<Category>>(json, JsonOptions);
                    if (categories != null && categories.Count > 0) return categories;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageService] LoadCategories failed: {ex.Message}");
            }
            return CreateDefaultCategories();
        }

        /// <summary>
        /// 安全持久化分类及快捷方式数据至 data/shortcuts.json（原子覆盖写入）
        /// </summary>
        /// <param name="categories">分类列表</param>
        public void SaveCategories(List<Category> categories)
        {
            try
            {
                EnsureDirectoriesExist();
                string json = JsonSerializer.Serialize(categories, JsonOptions);
                string tempFile = ShortcutsFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, ShortcutsFilePath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageService] SaveCategories failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建并持久化快快启动初始预置分类模板（常用工具、办公常用、网络浏览）
        /// </summary>
        /// <returns>默认分类列表</returns>
        public List<Category> CreateDefaultCategories()
        {
            var list = new List<Category>();

            // 常用工具
            var toolCategory = new Category { Name = "常用工具", IconSymbol = "Wrench24", OrderIndex = 0 };
            var sysGroup = new ShortcutGroup { Name = "系统常用", OrderIndex = 0, IsExpanded = true };
            sysGroup.Items.Add(new ShortcutItem { Name = "资源管理器", TargetPath = "explorer.exe", ItemType = ShortcutType.Application, Description = "打开 Windows 资源管理器" });
            sysGroup.Items.Add(new ShortcutItem { Name = "记事本", TargetPath = "notepad.exe", ItemType = ShortcutType.Application, Description = "打开记事本" });
            sysGroup.Items.Add(new ShortcutItem { Name = "计算器", TargetPath = "calc.exe", ItemType = ShortcutType.Application, Description = "打开计算器" });
            toolCategory.Groups.Add(sysGroup);
            list.Add(toolCategory);

            // 办公常用
            var officeCategory = new Category { Name = "办公常用", IconSymbol = "Briefcase24", OrderIndex = 1 };
            officeCategory.Groups.Add(new ShortcutGroup { Name = "办公软件", OrderIndex = 0, IsExpanded = true });
            list.Add(officeCategory);

            // 网络浏览
            var webCategory = new Category { Name = "网络浏览", IconSymbol = "Globe24", OrderIndex = 2 };
            var webGroup = new ShortcutGroup { Name = "常用网址", OrderIndex = 0, IsExpanded = true };
            webGroup.Items.Add(new ShortcutItem { Name = "百度一下", TargetPath = "https://www.baidu.com", ItemType = ShortcutType.Url, Description = "全球最大的中文搜索引擎" });
            webGroup.Items.Add(new ShortcutItem { Name = "GitHub", TargetPath = "https://github.com", ItemType = ShortcutType.Url, Description = "Where the world builds software" });
            webCategory.Groups.Add(webGroup);
            list.Add(webCategory);

            SaveCategories(list);
            return list;
        }
        #endregion

        #region Settings Persistence
        /// <summary>
        /// 读取全局配置 settings.json；若不存在则自动初始化默认设置
        /// </summary>
        /// <returns>软件全局设置模型</returns>
        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (settings != null) return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageService] LoadSettings failed: {ex.Message}");
            }

            var defaultSettings = new AppSettings();
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        /// <summary>
        /// 安全持久化全局设置（原子覆盖写入）
        /// </summary>
        /// <param name="settings">设置模型</param>
        public void SaveSettings(AppSettings settings)
        {
            try
            {
                EnsureDirectoriesExist();
                string json = JsonSerializer.Serialize(settings, JsonOptions);
                string tempFile = SettingsFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, SettingsFilePath, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StorageService] SaveSettings failed: {ex.Message}");
            }
        }
        #endregion
    }
}
