using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 桌面快捷方式检测与创建管理服务
    /// </summary>
    public class DesktopShortcutService
    {
        private const string DefaultShortcutName = "快快启动.lnk";

        /// <summary>
        /// 获取当前应用程序物理绝对路径
        /// </summary>
        public virtual string? GetCurrentExecutablePath()
        {
            string? path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path))
            {
                try
                {
                    path = Process.GetCurrentProcess().MainModule?.FileName;
                }
                catch { }
            }
            return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path);
        }

        /// <summary>
        /// 检查当前用户桌面或公共桌面是否存在指向本程序的快捷方式
        /// </summary>
        /// <returns>存在返回 true，否则返回 false</returns>
        public virtual bool HasDesktopShortcut()
        {
            string? currentExe = GetCurrentExecutablePath();
            if (string.IsNullOrEmpty(currentExe))
            {
                return true; // 无法定位当前可执行文件时不提示
            }

            var desktopDirs = new List<string>();
            try
            {
                string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(userDesktop) && Directory.Exists(userDesktop))
                {
                    desktopDirs.Add(userDesktop);
                }
            }
            catch { }

            try
            {
                string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                if (!string.IsNullOrEmpty(commonDesktop) && Directory.Exists(commonDesktop))
                {
                    desktopDirs.Add(commonDesktop);
                }
            }
            catch { }

            foreach (var dir in desktopDirs)
            {
                try
                {
                    var lnkFiles = Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly);
                    foreach (var lnk in lnkFiles)
                    {
                        string target = ResolveShortcutTarget(lnk);
                        if (!string.IsNullOrEmpty(target))
                        {
                            try
                            {
                                if (string.Equals(Path.GetFullPath(target), currentExe, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DesktopShortcutService] Scan directory failed: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// 解析指定 .lnk 快捷方式的目标路径
        /// </summary>
        /// <param name="lnkPath">.lnk 文件完整物理路径</param>
        /// <returns>目标物理路径，解析失败返回空字符串</returns>
        public virtual string ResolveShortcutTarget(string lnkPath)
        {
            if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath))
            {
                return string.Empty;
            }

            try
            {
                var link = (IShellLinkW)new ShellLink();
                var file = (IPersistFile)link;
                file.Load(lnkPath, 0);

                var sbPath = new StringBuilder(260);
                link.GetPath(sbPath, sbPath.Capacity, out _, 0);
                return sbPath.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DesktopShortcutService] Resolve target failed: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 在桌面创建指向当前程序的快捷方式
        /// </summary>
        /// <param name="customShortcutPath">可选自定义快捷方式保存路径（主要用于单元测试）</param>
        /// <returns>创建成功返回 true，否则返回 false</returns>
        public virtual bool CreateDesktopShortcut(string? customShortcutPath = null)
        {
            try
            {
                string? currentExe = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(currentExe))
                {
                    return false;
                }

                string targetLnkPath;
                if (!string.IsNullOrEmpty(customShortcutPath))
                {
                    targetLnkPath = customShortcutPath;
                }
                else
                {
                    string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (string.IsNullOrEmpty(userDesktop) || !Directory.Exists(userDesktop))
                    {
                        return false;
                    }
                    targetLnkPath = Path.Combine(userDesktop, DefaultShortcutName);
                }

                string? workDir = Path.GetDirectoryName(currentExe);

                var link = (IShellLinkW)new ShellLink();
                link.SetPath(currentExe);
                if (!string.IsNullOrEmpty(workDir))
                {
                    link.SetWorkingDirectory(workDir);
                }
                link.SetDescription("快快启动 - 高效便捷的桌面启动与管理工具");
                link.SetIconLocation(currentExe, 0);

                var file = (IPersistFile)link;
                file.Save(targetLnkPath, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DesktopShortcutService] Create shortcut failed: {ex.Message}");
                return false;
            }
        }
    }
}
