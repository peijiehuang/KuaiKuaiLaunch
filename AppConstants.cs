using System;
using System.Diagnostics;

namespace KuaiKuaiLaunch
{
    /// <summary>
    /// 全局应用程序常量与工具定义
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// 应用程序名称
        /// </summary>
        public const string AppName = "快快启动";

        /// <summary>
        /// 应用程序版本号
        /// </summary>
        public const string AppVersion = "beta V0.0.1";

        /// <summary>
        /// 官方开源 GitHub 项目仓库地址
        /// </summary>
        public const string GitHubUrl = "https://github.com/peijiehuang/KuaiKuaiLaunch";

        /// <summary>
        /// 使用系统默认浏览器打开 GitHub 项目主页
        /// </summary>
        public static void OpenGitHub()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppConstants] OpenGitHub failed: {ex.Message}");
            }
        }
    }
}
