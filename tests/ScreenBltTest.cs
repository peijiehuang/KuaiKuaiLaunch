using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KuaiKuaiLaunch.Tests
{
    [TestClass]
    public class ScreenBltTest
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [STATestMethod]
        public void TestDesktopBlt()
        {
            IntPtr hDesk = GetDC(IntPtr.Zero);
            Console.WriteLine($"Desktop HDC: {hDesk}");
            if (hDesk == IntPtr.Zero) return;

            int w = 500;
            int h = 400;
            IntPtr hMemDC = CreateCompatibleDC(hDesk);
            IntPtr hBmp = CreateCompatibleBitmap(hDesk, w, h);
            IntPtr hOld = SelectObject(hMemDC, hBmp);

            bool success = BitBlt(hMemDC, 0, 0, w, h, hDesk, 100, 100, 0x00CC0020);
            Console.WriteLine($"BitBlt success: {success}");

            SelectObject(hMemDC, hOld);
            DeleteDC(hMemDC);
            ReleaseDC(IntPtr.Zero, hDesk);

            if (success)
            {
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                string assetsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\assets"));
                string path = Path.Combine(assetsDir, "screen_blt_test.png");
                using (var fs = File.Open(path, FileMode.Create))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(src));
                    enc.Save(fs);
                }
                Console.WriteLine($"Saved screen blt to: {path}");
            }
            DeleteObject(hBmp);
        }
    }
}
