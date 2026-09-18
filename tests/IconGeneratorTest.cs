using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KuaiKuaiLaunch.Tests
{
    /// <summary>
    /// 快快启动 Fluent 风格高质量多分辨率应用程序图标生成器
    /// </summary>
    [TestClass]
    public class IconGeneratorTest
    {
        [TestMethod]
        public void GenerateAppIconAssets()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string assetsDir = Path.Combine(projectRoot, "assets");
            if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

            int baseSize = 512;
            using var masterBmp = new Bitmap(baseSize, baseSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(masterBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // 1. 柔和环境外阴影
                int margin = 32;
                int cardSize = baseSize - margin * 2;
                int radius = 96;

                using (var shadowPath = CreateRoundedRectanglePath(new RectangleF(margin + 4, margin + 16, cardSize - 8, cardSize - 8), radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }

                // 2. 现代 Fluent 蓝紫渐变亚克力卡片底座
                var cardRect = new RectangleF(margin, margin, cardSize, cardSize);
                using (var cardPath = CreateRoundedRectanglePath(cardRect, radius))
                {
                    using (var cardBrush = new LinearGradientBrush(
                        new PointF(margin, margin),
                        new PointF(margin + cardSize, margin + cardSize),
                        Color.FromArgb(255, 37, 99, 235),
                        Color.FromArgb(255, 15, 23, 42)))
                    {
                        var cb = new ColorBlend(3)
                        {
                            Colors = new[]
                            {
                                Color.FromArgb(255, 14, 165, 233), // 顶角极速亮青天蓝
                                Color.FromArgb(255, 59, 130, 246), // 中部活力海蓝
                                Color.FromArgb(255, 30, 27, 75)    // 右下深邃暗夜靛蓝
                            },
                            Positions = new[] { 0.0f, 0.45f, 1.0f }
                        };
                        cardBrush.InterpolationColors = cb;
                        g.FillPath(cardBrush, cardPath);
                    }

                    // 3. 半透明微发光反光边框
                    using (var borderPen = new Pen(Color.FromArgb(140, 255, 255, 255), 4.5f))
                    {
                        g.DrawPath(borderPen, cardPath);
                    }

                    // 4. 玻璃高光光泽弧面
                    using (var sheenPath = new GraphicsPath())
                    {
                        sheenPath.AddArc(margin, margin, radius * 2, radius * 2, 180, 90);
                        sheenPath.AddLine(margin + radius, margin, margin + cardSize - radius, margin);
                        sheenPath.AddArc(margin + cardSize - radius * 2, margin, radius * 2, radius * 2, 270, 90);
                        sheenPath.AddLine(margin + cardSize, margin + radius, margin, margin + cardSize * 0.46f);
                        sheenPath.CloseFigure();

                        using var sheenBrush = new LinearGradientBrush(
                            new PointF(margin, margin),
                            new PointF(margin, margin + cardSize * 0.46f),
                            Color.FromArgb(55, 255, 255, 255),
                            Color.FromArgb(0, 255, 255, 255));
                        g.FillPath(sheenBrush, sheenPath);
                    }
                }

                // 5. 动感发光闪电（Lightning Flash）
                PointF[] boltPoints = new PointF[]
                {
                    new PointF(280, 90),   // 顶部极速尖端
                    new PointF(184, 268),  // 左侧内折折角
                    new PointF(260, 268),  // 腰部横向蓄势折角
                    new PointF(220, 428),  // 底部锐利尖端
                    new PointF(340, 236),  // 右侧突刺折角
                    new PointF(264, 236)   // 中上凹陷转折
                };

                // 闪电立体投影
                PointF[] shadowBolt = new PointF[boltPoints.Length];
                for (int i = 0; i < boltPoints.Length; i++)
                {
                    shadowBolt[i] = new PointF(boltPoints[i].X + 4, boltPoints[i].Y + 8);
                }
                using (var boltShadowBrush = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
                {
                    g.FillPolygon(boltShadowBrush, shadowBolt);
                }

                // 闪电外部暖金色霓虹微光晕
                using (var glowPen = new Pen(Color.FromArgb(90, 253, 224, 71), 18f)
                {
                    LineJoin = LineJoin.Round,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                })
                {
                    g.DrawPolygon(glowPen, boltPoints);
                }

                // 闪电主体渐变填充（金黄 -> 琥珀黄 -> 炽热亮橙）
                using (var boltBrush = new LinearGradientBrush(
                    new PointF(200, 90),
                    new PointF(260, 428),
                    Color.FromArgb(255, 254, 240, 138),
                    Color.FromArgb(255, 245, 158, 11)))
                {
                    var boltCb = new ColorBlend(3)
                    {
                        Colors = new[]
                        {
                            Color.FromArgb(255, 255, 255, 240), // 顶部亮白高光
                            Color.FromArgb(255, 250, 204, 21),  // 中段饱满金黄
                            Color.FromArgb(255, 234, 88, 12)    // 尾部能量炽橙
                        },
                        Positions = new[] { 0.0f, 0.45f, 1.0f }
                    };
                    boltBrush.InterpolationColors = boltCb;
                    g.FillPolygon(boltBrush, boltPoints);
                }

                // 闪电精细高光描边
                using (var boltBorder = new Pen(Color.FromArgb(230, 255, 255, 255), 2.8f)
                {
                    LineJoin = LineJoin.Round
                })
                {
                    g.DrawPolygon(boltBorder, boltPoints);
                }
            }

            // 保存 256x256 高清原图 PNG
            string pngPath = Path.Combine(assetsDir, "app.png");
            using var bmp256 = ResizeImage(masterBmp, 256, 256);
            bmp256.Save(pngPath, ImageFormat.Png);

            // 生成涵盖 256, 128, 64, 48, 32, 24, 16 分辨率的标准 Windows ICO 文件
            string icoPath = Path.Combine(assetsDir, "app.ico");
            int[] sizes = new[] { 256, 128, 64, 48, 32, 24, 16 };
            WriteMultiResolutionIco(masterBmp, sizes, icoPath);

            Assert.IsTrue(File.Exists(pngPath), "app.png 应已成功生成");
            Assert.IsTrue(File.Exists(icoPath), "app.ico 应已成功生成");
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Bitmap ResizeImage(Bitmap source, int width, int height)
        {
            var dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(dest);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(source, new Rectangle(0, 0, width, height));
            return dest;
        }

        private static void WriteMultiResolutionIco(Bitmap master, int[] sizes, string outputPath)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // 1. ICO Header
            bw.Write((ushort)0); // Reserved
            bw.Write((ushort)1); // Type 1 = Icon
            bw.Write((ushort)sizes.Length); // Count

            var pngDataList = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using var resized = ResizeImage(master, sizes[i], sizes[i]);
                using var imgMs = new MemoryStream();
                resized.Save(imgMs, ImageFormat.Png);
                pngDataList[i] = imgMs.ToArray();
            }

            int headerSize = 6 + (16 * sizes.Length);
            int currentOffset = headerSize;

            // 2. Directory Entries
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s == 256 ? 0 : s)); // Width
                bw.Write((byte)(s == 256 ? 0 : s)); // Height
                bw.Write((byte)0);                   // Colors
                bw.Write((byte)0);                   // Reserved
                bw.Write((ushort)1);                 // Color planes
                bw.Write((ushort)32);                // Bits per pixel
                bw.Write((uint)pngDataList[i].Length); // Image data bytes
                bw.Write((uint)currentOffset);       // Image data offset
                currentOffset += pngDataList[i].Length;
            }

            // 3. Image Raw PNG Data
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngDataList[i]);
            }

            File.WriteAllBytes(outputPath, ms.ToArray());
        }
    }
}
