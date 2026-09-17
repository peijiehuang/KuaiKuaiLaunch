using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 快捷方式解析服务（深度解析 Windows .lnk 快捷方式、.url 互联网快捷方式、文件夹及可执行文件）
    /// </summary>
    public class LnkParserService
    {
        private readonly StorageService _storageService;

        /// <summary>
        /// 构造函数，注入存储服务
        /// </summary>
        public LnkParserService(StorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// 解析拖拽入界面的路径并生成标准化 ShortcutItem 模型
        /// </summary>
        /// <param name="path">拖拽文件的物理路径或 URL 字符串</param>
        /// <returns>解析好的快捷方式模型</returns>
        public ShortcutItem ParseDroppedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            path = path.Trim('\"', ' ');

            // 1. Web 网址
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string title = "网页链接";
                try { title = new Uri(path).Host; } catch { }
                return new ShortcutItem { Name = title, TargetPath = path, ItemType = ShortcutType.Url };
            }

            // 2. 本地物理目录
            if (Directory.Exists(path))
            {
                string dirName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(dirName)) dirName = path;
                return new ShortcutItem
                {
                    Name = dirName,
                    TargetPath = _storageService.ToPortablePath(path),
                    ItemType = ShortcutType.Folder,
                    WorkingDirectory = _storageService.ToPortablePath(path)
                };
            }

            // 3. Windows .url 互联网快捷方式
            if (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return ParseInternetShortcut(path);
            }

            // 4. Windows .lnk 快捷方式文件
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return ParseShellLink(path);
            }

            // 5. 普通文件或应用程序
            string fileName = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            ShortcutType type = (ext == ".exe" || ext == ".bat" || ext == ".cmd" || ext == ".ps1") ? ShortcutType.Application : ShortcutType.File;
            string? dir = Path.GetDirectoryName(path);

            return new ShortcutItem
            {
                Name = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(path) : fileName,
                TargetPath = _storageService.ToPortablePath(path),
                ItemType = type,
                WorkingDirectory = string.IsNullOrWhiteSpace(dir) ? string.Empty : _storageService.ToPortablePath(dir)
            };
        }

        /// <summary>
        /// 读取 INI 格式的 .url 快捷方式，提取真实的 URL 目标和图标位置
        /// </summary>
        private ShortcutItem ParseInternetShortcut(string urlFilePath)
        {
            string url = string.Empty;
            string iconFile = string.Empty;
            string name = Path.GetFileNameWithoutExtension(urlFilePath);

            try
            {
                var lines = File.ReadAllLines(urlFilePath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) url = line.Substring(4).Trim();
                    else if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) iconFile = line.Substring(9).Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LnkParser] Failed to read .url file: {ex.Message}");
            }

            return new ShortcutItem
            {
                Name = name,
                TargetPath = string.IsNullOrWhiteSpace(url) ? urlFilePath : url,
                ItemType = ShortcutType.Url,
                CustomIconPath = iconFile
            };
        }

        /// <summary>
        /// 基于 COM 接口（IShellLinkW / IPersistFile）深度解析 .lnk 文件真实目标、参数与起始目录
        /// </summary>
        private ShortcutItem ParseShellLink(string lnkFilePath)
        {
            string name = Path.GetFileNameWithoutExtension(lnkFilePath);
            string targetPath = string.Empty;
            string arguments = string.Empty;
            string workingDir = string.Empty;
            string description = string.Empty;
            string iconLocation = string.Empty;

            try
            {
                var link = (IShellLinkW)new ShellLink();
                var file = (IPersistFile)link;
                file.Load(lnkFilePath, 0);

                var sbPath = new StringBuilder(260);
                link.GetPath(sbPath, sbPath.Capacity, out _, 0);
                targetPath = sbPath.ToString();

                var sbArgs = new StringBuilder(1024);
                link.GetArguments(sbArgs, sbArgs.Capacity);
                arguments = sbArgs.ToString();

                var sbDir = new StringBuilder(260);
                link.GetWorkingDirectory(sbDir, sbDir.Capacity);
                workingDir = sbDir.ToString();

                var sbDesc = new StringBuilder(1024);
                link.GetDescription(sbDesc, sbDesc.Capacity);
                description = sbDesc.ToString();

                var sbIcon = new StringBuilder(260);
                link.GetIconLocation(sbIcon, sbIcon.Capacity, out _);
                iconLocation = sbIcon.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LnkParser] ShellLink parse failed: {ex.Message}");
                targetPath = lnkFilePath;
            }

            if (string.IsNullOrWhiteSpace(targetPath)) targetPath = lnkFilePath;

            ShortcutType type = ShortcutType.Application;
            if (Directory.Exists(targetPath))
            {
                type = ShortcutType.Folder;
            }
            else
            {
                string ext = Path.GetExtension(targetPath).ToLowerInvariant();
                if (ext != ".exe" && ext != ".bat" && ext != ".cmd" && ext != ".ps1")
                {
                    type = ShortcutType.File;
                }
            }

            return new ShortcutItem
            {
                Name = name,
                TargetPath = _storageService.ToPortablePath(targetPath),
                Arguments = arguments,
                WorkingDirectory = _storageService.ToPortablePath(workingDir),
                Description = description,
                ItemType = type,
                CustomIconPath = iconLocation
            };
        }
    }
}
