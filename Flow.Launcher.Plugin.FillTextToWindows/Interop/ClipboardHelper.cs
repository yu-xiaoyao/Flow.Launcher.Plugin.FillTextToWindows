using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Flow.Launcher.Plugin.FillTextToWindows.Interop
{
    /// <summary>
    /// 直接操作 Win32 剪贴板，不依赖 WPF / WinForms，可在任意线程调用。
    /// </summary>
    internal static class ClipboardHelper
    {
        private const int OpenRetryCount = 10;

        private const int OpenRetryDelayMs = 20;

        /// <summary>
        /// 读取剪贴板中的文本，失败或剪贴板里不是文本时返回 null。
        /// </summary>
        public static string GetText()
        {
            if (!NativeMethods.IsClipboardFormatAvailable(NativeMethods.CF_UNICODETEXT))
            {
                return null;
            }

            if (!TryOpenClipboard())
            {
                return null;
            }

            try
            {
                var handle = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
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
                    return Marshal.PtrToStringUni(pointer);
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

        /// <summary>
        /// 写入文本到剪贴板（等价于手工按下 Ctrl+C 之外的那一步「复制」）。
        /// </summary>
        public static bool SetText(string text)
        {
            text ??= string.Empty;

            if (!TryOpenClipboard())
            {
                return false;
            }

            try
            {
                if (!NativeMethods.EmptyClipboard())
                {
                    return false;
                }

                var byteCount = (text.Length + 1) * sizeof(char);
                var global = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)byteCount);
                if (global == IntPtr.Zero)
                {
                    return false;
                }

                var ownershipTransferred = false;
                try
                {
                    var pointer = NativeMethods.GlobalLock(global);
                    if (pointer == IntPtr.Zero)
                    {
                        return false;
                    }

                    try
                    {
                        Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);

                        // 写入结尾的 NUL，CF_UNICODETEXT 要求以空字符结束
                        Marshal.WriteInt16(pointer, text.Length * sizeof(char), 0);
                    }
                    finally
                    {
                        NativeMethods.GlobalUnlock(global);
                    }

                    ownershipTransferred = NativeMethods.SetClipboardData(NativeMethods.CF_UNICODETEXT, global) != IntPtr.Zero;
                    return ownershipTransferred;
                }
                finally
                {
                    // SetClipboardData 成功后所有权归系统，不能再自己释放
                    if (!ownershipTransferred)
                    {
                        NativeMethods.GlobalFree(global);
                    }
                }
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>
        /// 单个剪贴板格式的原始字节。
        /// </summary>
        internal readonly struct ClipboardFormatData
        {
            public ClipboardFormatData(uint format, byte[] data)
            {
                Format = format;
                Data = data;
            }

            public uint Format { get; }

            public byte[] Data { get; }
        }

        /// <summary>
        /// 剪贴板内容的快照。<see cref="Capture"/> 时把每个格式的原始字节拷一份出来，<see cref="Restore"/> 时原样写回。
        /// </summary>
        internal sealed class ClipboardSnapshot
        {
            private ClipboardSnapshot(bool captured, List<ClipboardFormatData> formats)
            {
                Captured = captured;
                Formats = formats;
            }

            /// <summary>捕获时成功打开了剪贴板。打开失败说明没读到内容，这时候别去覆盖剪贴板。</summary>
            public bool Captured { get; }

            public List<ClipboardFormatData> Formats { get; }

            public static ClipboardSnapshot Failed { get; } = new(false, new List<ClipboardFormatData>());

            public static ClipboardSnapshot Of(List<ClipboardFormatData> formats) => new(true, formats);
        }

        /// <summary>单个格式最大拷贝这么多字节，挡住 GlobalSize 拿到脏值的情况。</summary>
        private const long MaxFormatBytes = 256L * 1024 * 1024;

        /// <summary>
        /// 把剪贴板里的所有格式拷成字节存下来，供 <see cref="Restore"/> 还原。
        /// 走裸 Win32，任意线程都能调（WPF 的 <c>System.Windows.Clipboard</c> 要求 STA，在后台线程上会抛 ThreadStateException）。
        /// </summary>
        public static ClipboardSnapshot Capture()
        {
            if (!TryOpenClipboard())
            {
                return ClipboardSnapshot.Failed;
            }

            try
            {
                var formats = new List<ClipboardFormatData>();
                var format = 0u;

                while ((format = NativeMethods.EnumClipboardFormats(format)) != 0)
                {
                    if (IsUncopyableFormat(format))
                    {
                        continue;
                    }

                    var data = ReadFormat(format);
                    if (data != null)
                    {
                        formats.Add(new ClipboardFormatData(format, data));
                    }
                }

                return ClipboardSnapshot.Of(formats);
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>
        /// 用快照覆盖剪贴板。捕获时没拿到内容（<see cref="ClipboardSnapshot.Captured"/> 为 false）或者本来就没有格式时直接放弃，
        /// 免得把剪贴板清空。
        /// </summary>
        public static bool Restore(ClipboardSnapshot snapshot)
        {
            if (snapshot is not { Captured: true } || snapshot.Formats.Count == 0)
            {
                return false;
            }

            if (!TryOpenClipboard())
            {
                return false;
            }

            try
            {
                if (!NativeMethods.EmptyClipboard())
                {
                    return false;
                }

                foreach (var item in snapshot.Formats)
                {
                    WriteFormat(item);
                }

                return true;
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }

        /// <summary>
        /// 读出某个格式的原始字节，读不了（不是 HGLOBAL、锁不住）就返回 null。
        /// </summary>
        private static byte[] ReadFormat(uint format)
        {
            var handle = NativeMethods.GetClipboardData(format);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var size = (long)NativeMethods.GlobalSize(handle);
            if (size <= 0 || size > MaxFormatBytes)
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
                var buffer = new byte[(int)size];
                Marshal.Copy(pointer, buffer, 0, buffer.Length);
                return buffer;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }

        private static void WriteFormat(ClipboardFormatData item)
        {
            var global = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (UIntPtr)item.Data.Length);
            if (global == IntPtr.Zero)
            {
                return;
            }

            var ownershipTransferred = false;
            try
            {
                var pointer = NativeMethods.GlobalLock(global);
                if (pointer == IntPtr.Zero)
                {
                    return;
                }

                try
                {
                    Marshal.Copy(item.Data, 0, pointer, item.Data.Length);
                }
                finally
                {
                    NativeMethods.GlobalUnlock(global);
                }

                ownershipTransferred =
                    NativeMethods.SetClipboardData(item.Format, global) != IntPtr.Zero;
            }
            finally
            {
                // SetClipboardData 成功后所有权归系统，不能再自己释放
                if (!ownershipTransferred)
                {
                    NativeMethods.GlobalFree(global);
                }
            }
        }

        /// <summary>
        /// 这些格式的数据是 GDI 句柄或者系统即时合成的，GetClipboardData 拿到的不是可拷贝的内存块，跳过。
        /// </summary>
        private static bool IsUncopyableFormat(uint format)
        {
            switch (format)
            {
                case NativeMethods.CF_BITMAP:
                case NativeMethods.CF_METAFILEPICT:
                case NativeMethods.CF_PALETTE:
                case NativeMethods.CF_ENHMETAFILE:
                case NativeMethods.CF_OWNERDISPLAY:
                case NativeMethods.CF_DSPTEXT:
                case NativeMethods.CF_DSPBITMAP:
                case NativeMethods.CF_DSPMETAFILEPICT:
                case NativeMethods.CF_DSPENHMETAFILE:
                    return true;
            }

            return (format >= NativeMethods.CF_PRIVATEFIRST && format <= NativeMethods.CF_PRIVATELAST)
                   || (format >= NativeMethods.CF_GDIOBJFIRST && format <= NativeMethods.CF_GDIOBJLAST);
        }

        /// <summary>
        /// 剪贴板经常被其它程序短暂占用，这里做几次重试。
        /// </summary>
        private static bool TryOpenClipboard()
        {
            for (var i = 0; i < OpenRetryCount; i++)
            {
                if (NativeMethods.OpenClipboard(IntPtr.Zero))
                {
                    return true;
                }

                Thread.Sleep(OpenRetryDelayMs);
            }

            return false;
        }
    }
}
