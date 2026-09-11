using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;

namespace Flow.Launcher.Plugin.FillTextToWindows.Interop
{
    /// <summary>
    /// 用 SendInput 模拟按下按键组合，例如 Ctrl+V、Tab、Shift+Tab。
    /// </summary>
    internal static class KeyboardSimulator
    {
        /// <summary>Ctrl+V，也就是「粘贴」。</summary>
        public static readonly KeyChord Paste = KeyChord.Create(KeyCodes.V, KeyCodes.Control);

        private static readonly int InputSize = Marshal.SizeOf<NativeMethods.INPUT>();

        /// <summary>同一组合内部按下 / 抬起之间的间隔，太短个别程序会漏键。</summary>
        private const int WithinChordDelayMs = 10;

        /// <summary>需要带 EXTENDEDKEY 标志才正确的键。</summary>
        private static readonly HashSet<ushort> ExtendedKeys = new()
        {
            KeyCodes.Insert,
            KeyCodes.Delete,
            KeyCodes.Home,
            KeyCodes.End,
            KeyCodes.PageUp,
            KeyCodes.PageDown,
            KeyCodes.Left,
            KeyCodes.Right,
            KeyCodes.Up,
            KeyCodes.Down,
            KeyCodes.NumLock,
            KeyCodes.PrintScreen,
            KeyCodes.Divide,
            KeyCodes.RightControl,
            KeyCodes.RightAlt,
            KeyCodes.LeftWindows,
            KeyCodes.RightWindows,
            KeyCodes.Apps,
            KeyCodes.Sleep,
            // 多媒体 / 浏览器键都是扩展键
            KeyCodes.BrowserBack,
            KeyCodes.BrowserForward,
            KeyCodes.BrowserRefresh,
            KeyCodes.BrowserStop,
            KeyCodes.BrowserSearch,
            KeyCodes.BrowserFavorites,
            KeyCodes.BrowserHome,
            KeyCodes.VolumeMute,
            KeyCodes.VolumeDown,
            KeyCodes.VolumeUp,
            KeyCodes.MediaNext,
            KeyCodes.MediaPrev,
            KeyCodes.MediaStop,
            KeyCodes.MediaPlay,
            KeyCodes.LaunchMail,
            KeyCodes.LaunchMedia,
            KeyCodes.LaunchApp1,
            KeyCodes.LaunchApp2,
        };

        /// <summary>
        /// 依次发送一组按键组合。
        /// </summary>
        /// <returns>全部发送成功返回 true。</returns>
        public static bool Send(IReadOnlyList<KeyChord> chords)
        {
            if (chords == null)
            {
                return true;
            }

            var allSucceeded = true;

            foreach (var chord in chords)
            {
                allSucceeded &= Send(chord);
                Thread.Sleep(WithinChordDelayMs);
            }

            return allSucceeded;
        }

        /// <summary>
        /// 发送单个按键组合：先按修饰键，再按主键，然后逆序抬起。
        /// </summary>
        public static bool Send(KeyChord chord)
        {
            var inputs = new List<NativeMethods.INPUT>(chord.Modifiers.Length * 2 + 2);

            foreach (var modifier in chord.Modifiers)
            {
                inputs.Add(CreateKeyInput(modifier, keyUp: false));
            }

            inputs.Add(CreateKeyInput(chord.VirtualKey, keyUp: false));
            inputs.Add(CreateKeyInput(chord.VirtualKey, keyUp: true));

            for (var i = chord.Modifiers.Length - 1; i >= 0; i--)
            {
                inputs.Add(CreateKeyInput(chord.Modifiers[i], keyUp: true));
            }

            var sent = NativeMethods.SendInput((uint)inputs.Count, inputs.ToArray(), InputSize);
            return sent == inputs.Count;
        }

        private static NativeMethods.INPUT CreateKeyInput(ushort virtualKey, bool keyUp)
        {
            var flags = 0u;

            if (keyUp)
            {
                flags |= NativeMethods.KEYEVENTF_KEYUP;
            }

            if (ExtendedKeys.Contains(virtualKey))
            {
                flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            }

            return new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = virtualKey,
                        wScan = (ushort)NativeMethods.MapVirtualKey(virtualKey, NativeMethods.MAPVK_VK_TO_VSC),
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }
    }
}
