using System;
using System.Diagnostics;
using System.IO;
using KuaiKuaiLaunch.Models;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 快捷方式执行与启动分发服务（支持应用程序、系统内置命令、网址及文件夹）
    /// </summary>
    public class LauncherService
    {
        private readonly StorageService _storageService;

        /// <summary>
        /// 构造函数，注入存储服务
        /// </summary>
        public LauncherService(StorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// 执行启动目标项目（支持提权运行、参数展开及启动计数统计）
        /// </summary>
        /// <param name="item">快捷项模型</param>
        /// <param name="errorMessage">错误信息输出</param>
        /// <returns>是否成功启动</returns>
        public bool Launch(ShortcutItem item, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                string rawTarget = item.TargetPath?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    errorMessage = "目标路径为空";
                    return false;
                }

                // 1. 判定是否为 Web 网址或自定义 URI 协议
                bool isUrl = item.ItemType == ShortcutType.Url ||
                             rawTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             rawTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                             rawTarget.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                             rawTarget.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

                string target;
                string workingDir;

                if (isUrl)
                {
                    target = rawTarget;
                    if (target.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) target = "https://" + target;
                    else if (!target.Contains("://") && !target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) target = "https://" + target;
                    workingDir = string.Empty;
                }
                else
                {
                    target = _storageService.ResolvePath(rawTarget);
                    workingDir = _storageService.ResolvePath(item.WorkingDirectory);

                    // 若未指定工作目录，且目标是本地物理文件，则默认工作目录为文件所在父文件夹
                    if (string.IsNullOrWhiteSpace(workingDir) && File.Exists(target))
                    {
                        workingDir = Path.GetDirectoryName(target) ?? string.Empty;
                    }
                }

                string arguments = string.IsNullOrWhiteSpace(item.Arguments) ? string.Empty : Environment.ExpandEnvironmentVariables(item.Arguments);
                var psi = new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = true
                };

                if (item.RunAsAdmin && !isUrl)
                {
                    psi.Verb = "runas";
                }

                Process.Start(psi);

                // 更新启动统计指标
                item.LaunchCount++;
                item.LastLaunchTime = DateTime.Now;
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                errorMessage = "用户取消了管理员授权";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 在 Windows 资源管理器中打开并选中该快捷方式所指向的物理文件或所在目录
        /// </summary>
        /// <param name="item">快捷项模型</param>
        public void OpenFileLocation(ShortcutItem item)
        {
            try
            {
                if (item.ItemType == ShortcutType.Url) return;

                string target = _storageService.ResolvePath(item.TargetPath);
                if (File.Exists(target))
                {
                    Process.Start("explorer.exe", $"/select,\"{target}\"");
                }
                else if (Directory.Exists(target))
                {
                    Process.Start("explorer.exe", $"\"{target}\"");
                }
                else if (!string.IsNullOrWhiteSpace(item.WorkingDirectory))
                {
                    string dir = _storageService.ResolvePath(item.WorkingDirectory);
                    if (Directory.Exists(dir))
                    {
                        Process.Start("explorer.exe", $"\"{dir}\"");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LauncherService] OpenFileLocation error: {ex.Message}");
            }
        }
    }
}
