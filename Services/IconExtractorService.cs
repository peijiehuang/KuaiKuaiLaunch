using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services.Win32;

namespace KuaiKuaiLaunch.Services
{
    /// <summary>
    /// 高清系统图标提取与本地 PNG 缓存服务（支持 Jumbo 256x256 / ExtraLarge 48x48 提取与无文件锁加载）
    /// </summary>
    public class IconExtractorService
    {
        private readonly StorageService _storageService;
        private static readonly Guid ImageListGuid = new("46EB5926-682E-4239-A187-B6745C9F0FB1");

        /// <summary>
        /// 构造函数，注入存储服务
        /// </summary>
        public IconExtractorService(StorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// 从快捷项目标文件、自定义图标路径或提示源提取高清图标，并保存至本地 data/icons/{itemId}.png
        /// </summary>
        /// <param name="item">快捷项模型</param>
        /// <param name="sourceHintPath">源文件提示路径（如 .lnk 解析前的原始路径）</param>
        /// <returns>保存后的 PNG 绝对路径，失败则返回空字符串</returns>
        public string ExtractAndSaveIcon(ShortcutItem item, string? sourceHintPath = null)
        {
            try
            {
                _storageService.EnsureDirectoriesExist();
                string iconFileName = $"{item.Id}.png";
                string targetPngPath = Path.Combine(_storageService.IconsDirectory, iconFileName);

                string pathToExtract = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.CustomIconPath))
                {
                    pathToExtract = _storageService.ResolvePath(item.CustomIconPath);
                }

                if (string.IsNullOrWhiteSpace(pathToExtract) || !File.Exists(pathToExtract))
                {
                    if (!string.IsNullOrWhiteSpace(sourceHintPath) && File.Exists(sourceHintPath)) pathToExtract = sourceHintPath;
                    else pathToExtract = _storageService.ResolvePath(item.TargetPath);
                }

                BitmapSource? bitmapSource = null;
                if (item.ItemType == ShortcutType.Url && !File.Exists(pathToExtract))
                {
                    bitmapSource = GetDefaultBrowserIcon();
                }
                else if (item.ItemType == ShortcutType.Folder || Directory.Exists(pathToExtract))
                {
                    bitmapSource = GetFolderIcon(pathToExtract);
                }
                else if (File.Exists(pathToExtract))
                {
                    bitmapSource = ExtractJumboOrLargeIcon(pathToExtract);
                }

                if (bitmapSource != null)
                {
                    SaveBitmapSourceToPng(bitmapSource, targetPngPath);
                    item.IconPath = $"icons/{iconFileName}";
                    return targetPngPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconExtractor] Extract error: {ex.Message}");
            }

            return string.Empty;
        }

        /// <summary>
        /// 加载已缓存或指定的图标为 WPF ImageSource（使用 OnLoad 模式完全解绑文件锁）
        /// </summary>
        /// <param name="item">快捷项模型</param>
        /// <returns>WPF ImageSource 图标源，失败则返回 null</returns>
        public ImageSource? LoadIconImage(ShortcutItem item)
        {
            try
            {
                string iconFullPath = string.Empty;
                if (!string.IsNullOrWhiteSpace(item.IconPath))
                {
                    iconFullPath = Path.Combine(_storageService.DataDirectory, item.IconPath);
                }

                if (File.Exists(iconFullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(iconFullPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }

                // 本地缓存尚未生成，尝试实时提取一次
                string savedPath = ExtractAndSaveIcon(item);
                if (File.Exists(savedPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(savedPath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconExtractor] LoadIconImage error: {ex.Message}");
            }

            return null;
        }

        #region Private Extraction Helpers
        /// <summary>
        /// 优先尝试通过 Shell ImageList 提取 256x256 (JUMBO) 或 48x48 (EXTRALARGE) 高清图标
        /// </summary>
        private BitmapSource? ExtractJumboOrLargeIcon(string filePath)
        {
            var shinfo = new NativeMethods.SHFILEINFO();
            IntPtr hImgList = NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_SYSICONINDEX);

            if (hImgList != IntPtr.Zero && shinfo.iIcon >= 0)
            {
                var jumboBmp = GetIconFromSysImageList(NativeMethods.SHIL_JUMBO, shinfo.iIcon);
                if (jumboBmp != null) return jumboBmp;

                var extraLargeBmp = GetIconFromSysImageList(NativeMethods.SHIL_EXTRALARGE, shinfo.iIcon);
                if (extraLargeBmp != null) return extraLargeBmp;
            }

            // 备用降级方案 1: ExtractIconEx
            IntPtr[] largeIcons = new IntPtr[1];
            uint count = NativeMethods.ExtractIconEx(filePath, 0, largeIcons, null, 1);
            if (count > 0 && largeIcons[0] != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(largeIcons[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    NativeMethods.DestroyIcon(largeIcons[0]);
                }
            }

            // 备用降级方案 2: SHGetFileInfo Large Icon
            NativeMethods.SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }

            return null;
        }

        /// <summary>
        /// 提取系统文件夹目录的标准大图标
        /// </summary>
        private BitmapSource? GetFolderIcon(string folderPath)
        {
            var shinfo = new NativeMethods.SHFILEINFO();
            NativeMethods.SHGetFileInfo(folderPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_LARGEICON);
            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bmp = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    bmp.Freeze();
                    return bmp;
                }
                finally
                {
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                }
            }
            return null;
        }

        /// <summary>
        /// 提取系统默认浏览器（如 Microsoft Edge / Chrome）的图标作为网页快捷方式的默认呈现
        /// </summary>
        private BitmapSource? GetDefaultBrowserIcon()
        {
            try
            {
                string edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft\\Edge\\Application\\msedge.exe");
                if (!File.Exists(edgePath))
                {
                    edgePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft\\Edge\\Application\\msedge.exe");
                }
                if (File.Exists(edgePath)) return ExtractJumboOrLargeIcon(edgePath);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 从系统原生图像列表（SHGetImageList）句柄中解构并提取指定尺寸的 HICON 转换至 BitmapSource
        /// </summary>
        private BitmapSource? GetIconFromSysImageList(int sizeFlag, int iconIndex)
        {
            try
            {
                Guid iid = ImageListGuid;
                int hres = NativeMethods.SHGetImageList(sizeFlag, ref iid, out IImageList imageList);
                if (hres == 0 && imageList != null)
                {
                    IntPtr hIcon = IntPtr.Zero;
                    int hr = imageList.GetIcon(iconIndex, NativeMethods.ILD_TRANSPARENT, ref hIcon);
                    if (hr == 0 && hIcon != IntPtr.Zero)
                    {
                        try
                        {
                            var bmp = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            bmp.Freeze();
                            return bmp;
                        }
                        finally
                        {
                            NativeMethods.DestroyIcon(hIcon);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 将 BitmapSource 序列化并以高质量 PNG 格式保存至本地磁盘
        /// </summary>
        private void SaveBitmapSourceToPng(BitmapSource bitmapSource, string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }
        #endregion
    }
}
