using System;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Plugin.FillTextToWindows.Interop
{
    /// <summary>
    /// 插件用到的 Win32 API 声明。为了不引入 WinForms / WPF 剪贴板依赖，
    /// 剪贴板和按键模拟都直接走 P/Invoke，这样在任何线程上都能安全调用。
    /// </summary>
    internal static class NativeMethods
    {
        // ---------- 剪贴板 ----------

        public const uint CF_UNICODETEXT = 13;

        // 下面这些格式 GetClipboardData 返回的要么是 GDI 句柄、要么是系统即时合成的，
        // 都不是可以拷走字节的 HGLOBAL，枚举剪贴板时跳过。
        public const uint CF_BITMAP = 2;

        public const uint CF_METAFILEPICT = 3;

        public const uint CF_PALETTE = 9;

        public const uint CF_ENHMETAFILE = 14;

        public const uint CF_OWNERDISPLAY = 0x0080;

        public const uint CF_DSPTEXT = 0x0081;

        public const uint CF_DSPBITMAP = 0x0082;

        public const uint CF_DSPMETAFILEPICT = 0x0083;

        public const uint CF_DSPENHMETAFILE = 0x008E;

        public const uint CF_PRIVATEFIRST = 0x0200;

        public const uint CF_PRIVATELAST = 0x02FF;

        public const uint CF_GDIOBJFIRST = 0x0300;

        public const uint CF_GDIOBJLAST = 0x03FF;

        public const uint GMEM_MOVEABLE = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetClipboardData(uint uFormat);

        /// <summary>按顺序枚举剪贴板里的格式，传 0 拿第一个，之后传上一次的返回值，返回 0 表示结束。</summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint EnumClipboardFormats(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalUnlock(IntPtr hMem);

        // ---------- 按键模拟 ----------

        public const uint INPUT_KEYBOARD = 1;

        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint MAPVK_VK_TO_VSC = 0;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        /// <summary>
        /// 取某个键此刻按没按下。传 <see cref="Keys.KeyCodes.Control"/> 这类通用键码时，
        /// 左右两个 Ctrl（Shift / Alt 也一样）按住哪个都算按下，省得自己归一化。
        /// </summary>
        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;

            [FieldOffset(0)]
            public KEYBDINPUT ki;

            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }
}
