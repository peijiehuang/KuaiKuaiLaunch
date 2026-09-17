using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 开机自启动注册表服务（基于 HKCU\Software\Microsoft\Windows\CurrentVersion\Run）
    /// </summary>
    public class AutoStartService
    {
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "KuaiKuaiLaunch";

        /// <summary>
        /// 检查当前是否已启用开机自启动
        /// </summary>
        /// <returns>是否已配置注册表自启项</returns>
        public bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                if (key != null)
                {
                    var val = key.GetValue(AppName);
                    return val != null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoStartService] Check failed: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 开启或关闭开机自启动
        /// </summary>
        /// <param name="enable">true 开启，false 关闭</param>
        public void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath))
                    {
                        exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    }
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoStartService] Set failed: {ex.Message}");
            }
        }
    }
}
