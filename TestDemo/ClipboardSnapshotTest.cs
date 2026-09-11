using System.Runtime.InteropServices;
using System.Text;
using Flow.Launcher.Plugin.FillTextToWindows.Interop;

namespace TestDemo
{
    /// <summary>
    /// <see cref="ClipboardHelper"/> 的快照 / 还原测试。
    /// <para>
    /// 重点在于整段流程都跑在 MTA 后台线程上：之前 FillTextHelper 用的是 WPF 的
    /// <c>System.Windows.Clipboard</c>，它要求 STA，在 <c>Task.Run</c> 的线程池线程上会抛
    /// <c>ThreadStateException: Current thread must be set to single thread apartment (STA) mode before OLE calls can be made.</c>
    /// </para>
    /// <para>运行：<c>dotnet run --project TestDemo -- --clipboard-test</c></para>
    /// </summary>
    internal static class ClipboardSnapshotTest
    {
        private const uint CF_UNICODETEXT = 13;

        private const uint GMEM_MOVEABLE = 0x0002;

        /// <summary>自定义格式名，用来验证「非文本格式也能一起还原」。</summary>
        private const string CustomFormatName = "FillTextToWindows.TestFormat";

        private const string OriginText = "原始文本";

        private static readonly byte[] OriginBlob = { 1, 2, 3, 4, 5 };

        private static readonly List<string> Failures = new();

        private static Exception? _backgroundException;

        public static int Run()
        {
            // 整段流程都在 MTA 后台线程上跑，等价于插件里 Task.Run 的执行环境
            Case("后台（MTA）线程上捕获 / 还原不抛异常", () =>
            {
                Assert(Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA, "测试线程不是 MTA，验证不到点子上");
            });

            Case("文本 + 自定义格式一起还原", MultiFormatRoundTrip);
            Case("剪贴板本来就是空的时候不去清空它", EmptyClipboardIsLeftAlone);
            Case("还原之后可以继续正常写入", RestoreThenWriteAgain);

            Console.WriteLine();
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

        /// <summary>
        /// 模拟一次填充：先存一份剪贴板，填充过程中把它改成要粘贴的文本，结束后还原，检查原来那几份格式都回来了。
        /// </summary>
        private static void MultiFormatRoundTrip()
        {
            var customFormat = RegisterClipboardFormat(CustomFormatName);
            Assert(customFormat != 0, "RegisterClipboardFormat 失败了");

            SetClipboard(
                (CF_UNICODETEXT, EncodeText(OriginText)),
                (customFormat, OriginBlob));

            Assert(ClipboardHelper.GetText() == OriginText, "测试用的初始剪贴板没设置成功");

            var snapshot = ClipboardHelper.Capture();
            Assert(snapshot.Captured, "捕获失败");
            Assert(snapshot.Formats.Count >= 2, $"只捕获到 {snapshot.Formats.Count} 个格式，应该至少有文本和自定义格式两个");

            // 填充过程中会这么写
            Assert(ClipboardHelper.SetText("要被粘贴的内容"), "写入待粘贴文本失败");
            Assert(ClipboardHelper.GetText() == "要被粘贴的内容", "待粘贴文本没写进去");

            Assert(ClipboardHelper.Restore(snapshot), "还原失败");

            Assert(ClipboardHelper.GetText() == OriginText, $"文本没还原回来：{ClipboardHelper.GetText()}");
            Assert(ReadFormat(customFormat) is { } blob && blob.SequenceEqual(OriginBlob), "自定义格式没还原回来");
        }

        /// <summary>
        /// 剪贴板本来就是空的（没捕获到任何格式），这时候别去覆盖剪贴板，否则会把填充刚写进去的内容清掉。
        /// </summary>
        private static void EmptyClipboardIsLeftAlone()
        {
            SetClipboard();

            var snapshot = ClipboardHelper.Capture();
            Assert(snapshot.Captured, "空剪贴板也应该算捕获成功");
            Assert(snapshot.Formats.Count == 0, $"空剪贴板不该捕获到格式，实际 {snapshot.Formats.Count} 个");

            ClipboardHelper.SetText("填充后的内容");

            Assert(!ClipboardHelper.Restore(snapshot), "空快照不该执行还原");
            Assert(ClipboardHelper.GetText() == "填充后的内容", "空快照把剪贴板清掉了");
        }

        /// <summary>还原之后剪贴板要还能正常用。</summary>
        private static void RestoreThenWriteAgain()
        {
            SetClipboard((CF_UNICODETEXT, EncodeText("第一次")));

            var snapshot = ClipboardHelper.Capture();
            ClipboardHelper.SetText("第二次");
            Assert(ClipboardHelper.Restore(snapshot), "还原失败");

            Assert(ClipboardHelper.SetText("第三次"), "还原之后再写入失败");
            Assert(ClipboardHelper.GetText() == "第三次", "还原之后再写入的内容不对");
        }

        // ---------- 测试脚手架 ----------

        /// <summary>把一段流程丢到 MTA 线程上跑，等价于插件里 <c>Task.Run</c> 的执行环境。</summary>
        private static void Case(string name, Action body)
        {
            try
            {
                _backgroundException = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        // 确认真的是 MTA，不然这个测试证明不了什么
                        Assert(Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA, "后台线程不是 MTA");
                        body();
                    }
                    catch (Exception ex)
                    {
                        _backgroundException = ex;
                    }
                });

                thread.SetApartmentState(ApartmentState.MTA);
                thread.Start();
                thread.Join();

                if (_backgroundException != null)
                {
                    throw _backgroundException;
                }

                Console.WriteLine($"[通过] {name}");
            }
            catch (Exception ex)
            {
                Failures.Add($"{name}：{ex.Message}");
                Console.WriteLine($"[失败] {name}：{ex.Message}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>按 CF_UNICODETEXT 的规矩编码：UTF-16LE 加结尾 NUL。</summary>
        private static byte[] EncodeText(string text)
        {
            var chars = text.ToCharArray();
            var bytes = new byte[(chars.Length + 1) * sizeof(char)];
            Buffer.BlockCopy(chars, 0, bytes, 0, chars.Length * sizeof(char));
            return bytes;
        }

        /// <summary>用裸 Win32 铺一份剪贴板内容，不传参数就是清空。</summary>
        private static void SetClipboard(params (uint Format, byte[] Data)[] items)
        {
            if (!OpenClipboardForTest())
            {
                throw new InvalidOperationException("打开剪贴板失败");
            }

            try
            {
                if (!NativeMethods.EmptyClipboard())
                {
                    throw new InvalidOperationException("清空剪贴板失败");
                }

                foreach (var item in items)
                {
                    var global = NativeMethods.GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)item.Data.Length);
                    if (global == IntPtr.Zero)
                    {
                        throw new InvalidOperationException($"为格式 {item.Format} 分配内存失败");
                    }

                    var ownershipTransferred = false;
                    try
                    {
                        var pointer = NativeMethods.GlobalLock(global);
                        if (pointer == IntPtr.Zero)
                        {
                            throw new InvalidOperationException($"锁定格式 {item.Format} 的内存失败");
                        }

                        try
                        {
                            Marshal.Copy(item.Data, 0, pointer, item.Data.Length);
                        }
                        finally
                        {
                            NativeMethods.GlobalUnlock(global);
                        }

                        ownershipTransferred = NativeMethods.SetClipboardData(item.Format, global) != IntPtr.Zero;
                        if (!ownershipTransferred)
                        {
                            throw new InvalidOperationException($"写入格式 {item.Format} 失败");
                        }
                    }
                    finally
                    {
                        if (!ownershipTransferred)
                        {
                            NativeMethods.GlobalFree(global);
                        }
                    }
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        private static byte[]? ReadFormat(uint format)
        {
            if (!OpenClipboardForTest())
            {
                throw new InvalidOperationException("打开剪贴板失败");
            }

            try
            {
                var handle = NativeMethods.GetClipboardData(format);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                var pointer = NativeMethods.GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var bytes = new byte[(int)NativeMethods.GlobalSize(handle)];
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    return bytes;
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        /// <summary>
        /// 测试自己开剪贴板时多等一会儿：机器上别的剪贴板工具（Claude 的、输入法的、ClipboardPlus 之类）
        /// 会时不时占着剪贴板，等比 200ms 长一点，测试才不至于偶发失败。
        /// </summary>
        private static bool OpenClipboardForTest()
        {
            for (var i = 0; i < 100; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    return true;
                }

                Thread.Sleep(20);
            }

            return false;
        }
    }
}
