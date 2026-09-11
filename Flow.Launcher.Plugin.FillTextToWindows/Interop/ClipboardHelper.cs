using System;
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
