using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.FillTextToWindows.Keys
{
    /// <summary>
    /// 一个按键组合，例如 <c>Ctrl+V</c>、单独一个 <c>Tab</c>、或者加号键 <c>Ctrl+\+</c>。
    /// </summary>
    internal sealed class KeyChord
    {
        private KeyChord(ushort virtualKey, ushort[] modifiers)
        {
            VirtualKey = virtualKey;
            Modifiers = modifiers;
            Text = BuildText(virtualKey, modifiers);
        }

        /// <summary>主键键码。</summary>
        public ushort VirtualKey { get; }

        /// <summary>修饰键键码，已经按 Ctrl / Alt / Shift / Win 的顺序排好。</summary>
        public ushort[] Modifiers { get; }

        /// <summary>
        /// 规范写法，也是存进配置的那个字符串：<c>Ctrl+Shift+Tab</c>、<c>Ctrl+\+</c>。
        /// 键名里出现 <c>+</c>、<c>,</c>、<c>\</c> 时都带转义，所以再解析回来一定是同一个组合。
        /// </summary>
        public string Text { get; }

        /// <summary>给界面展示用的名字，就是 <see cref="Text"/>。</summary>
        public string DisplayName => Text;

        public static KeyChord Create(ushort virtualKey, params ushort[] modifiers)
        {
            return new KeyChord(virtualKey, Normalize(modifiers));
        }

        public override string ToString()
        {
            return Text;
        }

        /// <summary>
        /// 修饰键去重并按 Ctrl / Alt / Shift / Win 排序，这样同一个组合怎么写都长一个样。
        /// </summary>
        private static ushort[] Normalize(ushort[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return Array.Empty<ushort>();
            }

            return modifiers
                .Where(modifier => modifier != 0)
                .Distinct()
                .OrderBy(ModifierRank)
                .ToArray();
        }

        private static int ModifierRank(ushort code)
        {
            return code switch
            {
                KeyCodes.Control or KeyCodes.LeftControl or KeyCodes.RightControl => 0,
                KeyCodes.Alt or KeyCodes.LeftAlt or KeyCodes.RightAlt => 1,
                KeyCodes.Shift or KeyCodes.LeftShift or KeyCodes.RightShift => 2,
                KeyCodes.LeftWindows or KeyCodes.RightWindows => 3,
                _ => 4,
            };
        }

        private static string BuildText(ushort virtualKey, ushort[] modifiers)
        {
            var parts = new List<string>(modifiers.Length + 1);

            // 上档字符自己就代表 Shift，再单独写一个 Shift 反而绕
            // 比如 Ctrl+Shift+= 写成 Ctrl+\+，解析回来还是 Shift + = 这个键
            var shifted = modifiers.Contains(KeyCodes.Shift);
            var plainName = KeyCodes.GetName(virtualKey);
            var shiftedName = KeyCodes.GetName(virtualKey, withShift: true);
            var keyName = shifted ? shiftedName : plainName;
            var shiftCoveredByKeyName = shifted && !string.Equals(shiftedName, plainName, StringComparison.Ordinal);

            foreach (var modifier in modifiers)
            {
                if (modifier == KeyCodes.Shift && shiftCoveredByKeyName)
                {
                    continue;
                }

                parts.Add(KeyCodes.EscapeName(KeyCodes.GetName(modifier)));
            }

            parts.Add(KeyCodes.EscapeName(keyName));
            return string.Join("+", parts);
        }
    }
}
