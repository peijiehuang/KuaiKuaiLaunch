using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using KuaiKuaiLaunch.Models;
using KuaiKuaiLaunch.Services;

namespace KuaiKuaiLaunch.Tests
{
    /// <summary>
    /// 核心服务功能单元测试集合
    /// </summary>
    [TestClass]
    public class ServiceTests
    {
        /// <summary>
        /// 测试数据存储与默认分类生成加载
        /// </summary>
        [TestMethod]
        public void StorageService_ShouldCreateAndLoadDefaultCategories()
        {
            var storage = new StorageService();
            var categories = storage.LoadCategories();

            Assert.IsNotNull(categories);
            Assert.IsTrue(categories.Count >= 3, "默认分类应当包含至少 3 个分类");
            Assert.IsTrue(categories.Exists(c => c.Name == "常用工具"));
            Assert.IsTrue(categories.Exists(c => c.Name == "办公常用"));
            Assert.IsTrue(categories.Exists(c => c.Name == "网络浏览"));
        }

        /// <summary>
        /// 测试相对便携路径与绝对路径的互转与展开
        /// </summary>
        [TestMethod]
        public void StorageService_PortablePathConversion_ShouldWorkProperly()
        {
            var storage = new StorageService();
            string subPath = Path.Combine(storage.BaseDirectory, "tools", "test.exe");

            string portable = storage.ToPortablePath(subPath);
            Assert.IsTrue(portable.StartsWith("%APP_DIR%"), "属于程序子目录的文件应被转换为相对 %APP_DIR% 路径");

            string resolved = storage.ResolvePath(portable);
            Assert.AreEqual(Path.GetFullPath(subPath), Path.GetFullPath(resolved), "展开路径与原路径应完全一致");
        }

        /// <summary>
        /// 测试应用设置的 JSON 序列化与反序列化持久化
        /// </summary>
        [TestMethod]
        public void StorageService_SettingsPersistence_ShouldWork()
        {
            var storage = new StorageService();
            var settings = new AppSettings
            {
                AutoHideDelayMs = 450,
                EnableEdgeAutoHide = false,
                EnableMiddleClickWakeup = true,
                AlwaysOnTop = true,
                Theme = "Light",
                CheckDesktopShortcutOnStartup = false
            };

            storage.SaveSettings(settings);
            var loaded = storage.LoadSettings();

            Assert.AreEqual(450, loaded.AutoHideDelayMs);
            Assert.IsFalse(loaded.EnableEdgeAutoHide);
            Assert.IsTrue(loaded.EnableMiddleClickWakeup);
            Assert.IsTrue(loaded.AlwaysOnTop);
            Assert.AreEqual("Light", loaded.Theme);
            Assert.IsFalse(loaded.CheckDesktopShortcutOnStartup);
        }

        /// <summary>
        /// 测试路径解析时对网络网址及系统原生工具 (如 explorer.exe, notepad.exe) 的正确映射
        /// </summary>
        [TestMethod]
        public void StorageService_ResolvePath_ShouldHandleUrlsAndSystemCommands()
        {
            var storage = new StorageService();

            // 1. URLs should not be altered or combined with app directory
            Assert.AreEqual("https://www.baidu.com", storage.ResolvePath("https://www.baidu.com"));
            Assert.AreEqual("http://127.0.0.1:8080", storage.ResolvePath("http://127.0.0.1:8080"));

            // 2. System commands should resolve to valid physical system executables
            string explorer = storage.ResolvePath("explorer.exe");
            Assert.IsTrue(File.Exists(explorer), $"explorer.exe 应解析为系统物理存在的有效路径: {explorer}");

            string notepad = storage.ResolvePath("notepad.exe");
            Assert.IsTrue(File.Exists(notepad), $"notepad.exe 应解析为系统物理存在的有效路径: {notepad}");
        }

        /// <summary>
        /// 测试拖放解析服务对 URL、目录、普通文件的自动识别
        /// </summary>
        [TestMethod]
        public void LnkParserService_ShouldParseUrlsAndDirectories()
        {
            var storage = new StorageService();
            var parser = new LnkParserService(storage);

            // Test URL
            var urlItem = parser.ParseDroppedPath("https://vstartapp.com");
            Assert.AreEqual(ShortcutType.Url, urlItem.ItemType);
            Assert.AreEqual("vstartapp.com", urlItem.Name);
            Assert.AreEqual("https://vstartapp.com", urlItem.TargetPath);

            // Test Folder
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (Directory.Exists(winDir))
            {
                var folderItem = parser.ParseDroppedPath(winDir);
                Assert.AreEqual(ShortcutType.Folder, folderItem.ItemType);
                Assert.IsFalse(string.IsNullOrEmpty(folderItem.Name));
            }

            // Test File
            string notepadPath = Path.Combine(winDir, "notepad.exe");
            if (File.Exists(notepadPath))
            {
                var appItem = parser.ParseDroppedPath(notepadPath);
                Assert.AreEqual(ShortcutType.Application, appItem.ItemType);
                Assert.AreEqual("notepad", appItem.Name);
            }
        }

        /// <summary>
        /// 测试高清大图标提取与本地 PNG 缓存生成
        /// </summary>
        [TestMethod]
        public void IconExtractorService_ShouldExtractAndSaveIcon()
        {
            var storage = new StorageService();
            var iconSvc = new IconExtractorService(storage);

            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string notepadPath = Path.Combine(winDir, "notepad.exe");

            if (File.Exists(notepadPath))
            {
                var item = new ShortcutItem
                {
                    Name = "notepad",
                    TargetPath = notepadPath,
                    ItemType = ShortcutType.Application
                };

                string savedIconPath = iconSvc.ExtractAndSaveIcon(item);
                Assert.IsFalse(string.IsNullOrEmpty(savedIconPath), "应成功提取并生成图标文件路径");
                Assert.IsTrue(File.Exists(savedIconPath), "缓存的图标 PNG 文件应物理存在于磁盘");

                var image = iconSvc.LoadIconImage(item);
                Assert.IsNotNull(image, "应能成功加载为 WPF ImageSource");
            }
        }

        /// <summary>
        /// 调试输出当前工作区与多显示器范围参数
        /// </summary>
        [TestMethod]
        public void MonitorInfo_WorkArea_Check()
        {
            var primaryWork = System.Windows.SystemParameters.WorkArea;
            Console.WriteLine($"SystemParameters.WorkArea: Left={primaryWork.Left}, Top={primaryWork.Top}, Width={primaryWork.Width}, Height={primaryWork.Height}, Right={primaryWork.Right}");
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                Console.WriteLine($"Screen {s.DeviceName}: Bounds={s.Bounds}, WorkingArea={s.WorkingArea}");
            }
        }

        /// <summary>
        /// 测试单实例互斥体争用检测与跨进程唤醒事件触发机制
        /// </summary>
        [TestMethod]
        public void SingleInstance_MutexAndWakeupEvent_ShouldWork()
        {
            string testMutexName = "Local\\Test_SingleInstance_Mutex_" + Guid.NewGuid().ToString("N");
            string testEventName = "Local\\Test_Wakeup_Event_" + Guid.NewGuid().ToString("N");

            // 1. 模拟主实例启动，获取互斥体所有权
            using (var mutexA = new Mutex(true, testMutexName, out bool createdNewA))
            {
                Assert.IsTrue(createdNewA, "第一个实例应当成功创建并获得互斥体所有权");

                // 2. 模拟第二个实例尝试启动，使用同一个名称
                using (var mutexB = new Mutex(true, testMutexName, out bool createdNewB))
                {
                    Assert.IsFalse(createdNewB, "第二个实例启动时应当检测到互斥体已存在，createdNew 应为 false");
                }

                // 3. 测试跨进程唤醒事件通知
                using (var wakeupEventA = new EventWaitHandle(false, EventResetMode.AutoReset, testEventName))
                {
                    bool eventSignaled = false;
                    using var signalReceived = new ManualResetEvent(false);

                    var reg = ThreadPool.RegisterWaitForSingleObject(
                        wakeupEventA,
                        (state, timedOut) =>
                        {
                            if (!timedOut)
                            {
                                eventSignaled = true;
                                signalReceived.Set();
                            }
                        },
                        null,
                        2000,
                        true);

                    // 第二个实例打开事件并发送唤醒信号
                    bool opened = EventWaitHandle.TryOpenExisting(testEventName, out var wakeupEventB);
                    Assert.IsTrue(opened, "应当能够打开主实例创建的命名唤醒事件");
                    using (wakeupEventB)
                    {
                        wakeupEventB!.Set();
                    }

                    bool waitSuccess = signalReceived.WaitOne(2000);
                    reg.Unregister(null);

                    Assert.IsTrue(waitSuccess, "主实例应当在超时时间内收到唤醒事件信号");
                    Assert.IsTrue(eventSignaled, "事件通知状态应被成功标记为 true");
                }

                mutexA.ReleaseMutex();
            }

            // 4. 主实例完全释放后，后续新实例应当能重新获取所有权
            using (var mutexC = new Mutex(true, testMutexName, out bool createdNewC))
            {
                Assert.IsTrue(createdNewC, "主实例释放后，新实例应当可以重新成功获取互斥体");
                mutexC.ReleaseMutex();
            }
        }

        /// <summary>
        /// 测试 DesktopShortcutService 创建快捷方式并能够精准解析其目标物理路径
        /// </summary>
        [TestMethod]
        public void DesktopShortcutService_CreateAndResolveShortcut_ShouldWorkProperly()
        {
            var shortcutService = new DesktopShortcutService();
            string? currentExe = shortcutService.GetCurrentExecutablePath();
            Assert.IsNotNull(currentExe, "当前运行环境应当能够解析出主进程可执行文件绝对路径");
            Assert.IsTrue(File.Exists(currentExe), $"当前可执行文件应当真实存在: {currentExe}");

            string tempDir = Path.Combine(Path.GetTempPath(), "KuaiKuaiLaunch_Tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempLnk = Path.Combine(tempDir, "测试快捷方式.lnk");

            try
            {
                // 1. 创建快捷方式
                bool created = shortcutService.CreateDesktopShortcut(tempLnk);
                Assert.IsTrue(created, "应当成功在目标路径创建快捷方式");
                Assert.IsTrue(File.Exists(tempLnk), "快捷方式文件在磁盘上应当存在");

                // 2. 解析快捷方式目标路径
                string resolvedTarget = shortcutService.ResolveShortcutTarget(tempLnk);
                Assert.IsFalse(string.IsNullOrWhiteSpace(resolvedTarget), "解析出的目标路径不应为空");
                Assert.AreEqual(Path.GetFullPath(currentExe), Path.GetFullPath(resolvedTarget), "解析出的快捷方式目标应当与当前可执行文件绝对路径一致");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }
    }
}

