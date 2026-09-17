using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KuaiKuaiLaunch.Tests
{
    [TestClass]
    public class ScreenshotGenerator
    {
        [STATestMethod]
        public void GenerateDarkFluentWindowScreenshot()
        {
            if (Application.Current == null)
            {
                var app = new Application();
                var themeDict = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark };
                var controlsDict = new Wpf.Ui.Markup.ControlsDictionary();
                app.Resources.MergedDictionaries.Add(themeDict);
                app.Resources.MergedDictionaries.Add(controlsDict);
            }

            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);

            var win = new MainWindow();
            win.Width = 530;
            win.Height = 680;

            // Apply Dark Acrylic backdrop background for rendering
            win.Background = new SolidColorBrush(Color.FromArgb(245, 28, 33, 40));

            win.Show();

            // Pump dispatcher events to ensure all templates, data bindings and icons load
            for (int i = 0; i < 8; i++)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => { frame.Continue = false; }));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(80);
            }

            win.Measure(new Size(530, 680));
            win.Arrange(new Rect(0, 0, 530, 680));
            win.UpdateLayout();

            int w = (int)win.ActualWidth;
            int h = (int)win.ActualHeight;
            if (w <= 0) w = 530;
            if (h <= 0) h = 680;

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(win);

            string assetsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\assets"));
            Directory.CreateDirectory(assetsDir);
            string darkPath = Path.Combine(assetsDir, "app_actual_dark.png");
            using (var fs = File.Open(darkPath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(fs);
            }

            win.Close();
            Console.WriteLine($"Rendered dark window screenshot to: {darkPath}");
        }

        [STATestMethod]
        public void GenerateDualThemePoster()
        {
            string assetsDir = "";
            string curr = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                string test = Path.Combine(curr, "assets");
                if (Directory.Exists(test)) { assetsDir = test; break; }
                string? p = Path.GetDirectoryName(curr);
                if (p == null) break;
                curr = p;
            }
            if (string.IsNullOrEmpty(assetsDir)) assetsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\assets"));

            string darkImgPath = Path.Combine(assetsDir, "screenshot_dark.png");
            string lightImgPath = Path.Combine(assetsDir, "screenshot_light.png");
            string posterPath = Path.Combine(assetsDir, "poster.png");

            int posterW = 1600;
            int posterH = 900;

            // 1. Root Container with Apple-style studio neutral gradient
            var root = new System.Windows.Controls.Grid
            {
                Width = posterW,
                Height = posterH,
                Background = new LinearGradientBrush(
                    Color.FromRgb(248, 249, 251),
                    Color.FromRgb(236, 238, 243),
                    new Point(0.5, 0),
                    new Point(0.5, 1))
            };

            var mainStack = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            root.Children.Add(mainStack);

            // 2. Apple Minimalist Typography Header
            var headerStack = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 32)
            };

            var subHeader = new System.Windows.Controls.TextBlock
            {
                Text = "KUAIKUAI LAUNCH",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)), // Apple caption gray
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            headerStack.Children.Add(subHeader);

            var mainTitle = new System.Windows.Controls.TextBlock
            {
                Text = "快快启动",
                FontSize = 48,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)), // Apple text dark
                HorizontalAlignment = HorizontalAlignment.Center
            };
            headerStack.Children.Add(mainTitle);

            var tagLine = new System.Windows.Controls.TextBlock
            {
                Text = "轻快顺手，重归纯粹。",
                FontSize = 18,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            headerStack.Children.Add(tagLine);
            mainStack.Children.Add(headerStack);

            // 3. Central Screenshots Comparison (Perfect 3:4 aspect ratio: 450 x 600)
            var previewRow = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 32)
            };

            int imgW = 450;
            int imgH = 600;

            // Light Window Card
            var lightBorder = new System.Windows.Controls.Border
            {
                Width = imgW,
                Height = imgH,
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(30, 35, 48),
                    BlurRadius = 54,
                    ShadowDepth = 16,
                    Opacity = 0.16
                }
            };
            var lightBmp = new BitmapImage();
            lightBmp.BeginInit();
            lightBmp.UriSource = new Uri(lightImgPath, UriKind.Absolute);
            lightBmp.CacheOption = BitmapCacheOption.OnLoad;
            lightBmp.EndInit();
            lightBorder.Child = new System.Windows.Controls.Image
            {
                Source = lightBmp,
                Stretch = Stretch.UniformToFill
            };
            previewRow.Children.Add(lightBorder);

            // Breathing Space between windows
            var divider = new System.Windows.Controls.Border { Width = 64 };
            previewRow.Children.Add(divider);

            // Dark Window Card
            var darkBorder = new System.Windows.Controls.Border
            {
                Width = imgW,
                Height = imgH,
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(15, 20, 32),
                    BlurRadius = 60,
                    ShadowDepth = 18,
                    Opacity = 0.28
                }
            };
            var darkBmp = new BitmapImage();
            darkBmp.BeginInit();
            darkBmp.UriSource = new Uri(darkImgPath, UriKind.Absolute);
            darkBmp.CacheOption = BitmapCacheOption.OnLoad;
            darkBmp.EndInit();
            darkBorder.Child = new System.Windows.Controls.Image
            {
                Source = darkBmp,
                Stretch = Stretch.UniformToFill
            };
            previewRow.Children.Add(darkBorder);

            mainStack.Children.Add(previewRow);

            // 4. Subtle Apple-Style Bottom Features Line
            var footerLine = new System.Windows.Controls.TextBlock
            {
                Text = "参考经典音速启动  ·  深浅双模式自适应  ·  智能贴边隐藏  ·  鼠标中键秒唤醒",
                FontSize = 13.5,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainStack.Children.Add(footerLine);

            // 5. Measure, Arrange & Render
            root.Measure(new Size(posterW, posterH));
            root.Arrange(new Rect(0, 0, posterW, posterH));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(posterW, posterH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            using (var fs = File.Open(posterPath, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(fs);
            }

            Console.WriteLine($"Generated Apple-style poster successfully at: {posterPath}");
        }
    }
}
