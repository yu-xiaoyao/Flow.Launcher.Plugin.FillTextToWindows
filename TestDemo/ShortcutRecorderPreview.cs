using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Flow.Launcher.Plugin.FillTextToWindows.Interop;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;
using Flow.Launcher.Plugin.FillTextToWindows.Views;

// TestDemo 引了 WinForms，Button / ButtonBase / Application 都跟它撞名字，这里点名要 WPF 的
using Button = System.Windows.Controls.Button;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace TestDemo
{
    /// <summary>
    /// 把录制对话框真建出来跑一遍：XAML 解析不了、键盘图排歪了、按键录不进去，
    /// 都会在这一步露出来，不用装到 Flow Launcher 里点。最后截一张图。
    /// <para>运行：<c>dotnet run --project TestDemo -p:WithClipboardTest=true -- --recorder-preview [输出路径]</c></para>
    /// </summary>
    internal static class ShortcutRecorderPreview
    {
        /// <summary>一步步来，中间留出 Windows 分发按键消息的时间。</summary>
        private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(700);

        private static readonly List<string> Failures = new();

        public static int Run(string[] args)
        {
            var path = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "ftw-recorder-preview.png");

            // TestDemo 是 WinForms 工程，Application 得写全名，不然跟 WinForms 的撞
            var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnLastWindowClose };

            // 从空的开始，录到的每一条都得是这一步真的录进去的
            var viewModel = ShortcutRecorderViewModel.FromText(string.Empty);
            var window = new ShortcutRecorderWindow(viewModel, "预览");

            var step = 0;

            // 焦点不在我们窗口上的话一个键都不能发：tab / 回车会打到别人窗口里去
            var canSendKeys = false;

            var timer = new DispatcherTimer { Interval = StepInterval };
            timer.Tick += (_, _) =>
            {
                switch (step++)
                {
                    case 0:
                        // 物理按键那条路：用插件自己的 SendInput 封装发出去，走一遍系统再回来
                        window.Activate();
                        canSendKeys = window.IsActive;
                        if (!canSendKeys)
                        {
                            Failures.Add("窗口没拿到焦点，物理按键那条路这轮没验到");
                            break;
                        }

                        Send(KeyChord.Create(KeyCodes.V, KeyCodes.Control));
                        break;

                    case 1:
                        SendIfPossible(canSendKeys, KeyChord.Create(KeyCodes.Tab));
                        break;

                    case 2:
                        SendIfPossible(canSendKeys, KeyChord.Create(KeyCodes.Enter));
                        break;

                    case 3:
                        // Alt 组合在 WPF 里是 Key.System，真正的键在 SystemKey 里，单独验一下这条岔路
                        SendIfPossible(canSendKeys, KeyChord.Create(KeyCodes.V, KeyCodes.Alt));
                        break;

                    case 4:
                        // 键盘图那条路：点修饰键是「按住」，再点一个普通键记一条
                        ClickCap(window, "Ctrl");
                        ClickCap(window, "5");
                        break;

                    default:
                        timer.Stop();
                        Check(viewModel);
                        Save(window, path);
                        window.Close();
                        break;
                }
            };

            timer.Start();
            application.Run(window);

            Describe(window, path);

            if (Failures.Count == 0)
            {
                Console.WriteLine("全部通过。");
                return 0;
            }

            Console.WriteLine($"失败 {Failures.Count} 项：");
            foreach (var failure in Failures)
            {
                Console.WriteLine("  - " + failure);
            }

            return 1;
        }

        private static void SendIfPossible(bool canSendKeys, KeyChord chord)
        {
            if (canSendKeys)
            {
                Send(chord);
            }
        }

        private static void Send(KeyChord chord)
        {
            if (!KeyboardSimulator.Send(chord))
            {
                Failures.Add($"发送 {chord.Text} 失败");
            }
        }

        /// <summary>点一下键盘图上的键帽，跟用户用鼠标点是一样的。</summary>
        private static void ClickCap(Window window, string label)
        {
            var host = (Grid)window.FindName("KeyboardHost");

            var cap = host.Children
                .OfType<Button>()
                .FirstOrDefault(button => button.Tag is KeyCap keyCap && keyCap.Label == label);

            if (cap == null)
            {
                Failures.Add($"键盘图上找不到「{label}」这个键帽");
                return;
            }

            cap.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, cap));
        }

        private static void Check(ShortcutRecorderViewModel viewModel)
        {
            string[] expected = { "Ctrl+V", "Tab", "Enter", "Alt+V", "Ctrl+5" };
            var actual = viewModel.Result.ToArray();

            Console.WriteLine($"录到：{string.Join(" / ", actual)}");

            if (!actual.SequenceEqual(expected))
            {
                Failures.Add($"录到的是「{string.Join(" / ", actual)}」，应该是「{string.Join(" / ", expected)}」");
            }
        }

        private static void Save(Window window, string path)
        {
            var root = (FrameworkElement)window.Content;

            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(root.ActualWidth),
                (int)Math.Ceiling(root.ActualHeight),
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(path);
            encoder.Save(stream);
        }

        /// <summary>
        /// 排版对不对得看数字：键盘图比窗口宽的话右边会被裁掉，比窗口高的话按钮会被裁掉。
        /// </summary>
        private static void Describe(Window window, string path)
        {
            var scroller = (ScrollViewer)window.FindName("KeyboardScroller");
            var host = (FrameworkElement)window.FindName("KeyboardHost");

            Console.WriteLine($"窗口   {window.ActualWidth:0} x {window.ActualHeight:0}");
            Console.WriteLine($"滚动区 {scroller.ActualWidth:0} x {scroller.ActualHeight:0}"
                              + $"  内容 {scroller.ExtentWidth:0} x {scroller.ExtentHeight:0}"
                              + $"  横向滚动条 {scroller.ComputedHorizontalScrollBarVisibility}");
            Console.WriteLine($"键盘图 {host.ActualWidth:0} x {host.ActualHeight:0}");
            Console.WriteLine($"截图   {path}");
        }
    }
}
