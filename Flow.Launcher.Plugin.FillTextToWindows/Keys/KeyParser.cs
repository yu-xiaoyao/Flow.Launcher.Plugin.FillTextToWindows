using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Flow.Launcher.Plugin.FillTextToWindows.Keys
{
    /// <summary>
    /// 把配置里的字符串解析成按键序列。
    /// <para>
    /// 一个组合内部用 <c>+</c> 连接修饰键和主键，<c>Ctrl+V</c>、<c>Shift+Tab</c>、<c>Ctrl + V</c> 都行。
    /// 要连按多个键就写多个组合，用逗号隔开：<c>Down, Down, Enter</c>；
    /// 存进配置时一个组合就是 JSON 数组里的一项：<c>["Down", "Down", "Enter"]</c>。
    /// </para>
    /// <para>
    /// 整个键盘都能写，键名就是那个键上的字符（<c>,</c> <c>-</c> <c>[</c> <c>F5</c> <c>Num7</c> …）。
    /// <c>+</c>、<c>,</c>、<c>\</c> 本身也是按键，写的时候前面加一个 <c>\</c> 转义：
    /// <c>Ctrl + \+</c> 是「按住 Ctrl 再按加号键」，保存下来就是 <c>Ctrl+\+</c>。
    /// </para>
    /// </summary>
    internal static class KeyParser
    {
        /// <summary>配置为空时的说明文字。</summary>
        public const string EmptyDescription = "不发送按键";

        private const char ChordSeparator = ',';

        private const char ModifierSeparator = '+';

        /// <summary>
        /// 解析按键配置并返回给用户看的说明；写法有误时返回带 ⚠ 的错误提示。
        /// </summary>
        public static string Describe(IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return EmptyDescription;
            }

            return DescribeText(Format(keys));
        }

        /// <summary>
        /// 解析编辑框里的原文并返回说明，写法有误时返回带 ⚠ 的错误提示。
        /// </summary>
        public static string DescribeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return EmptyDescription;
            }

            if (!TryParse(text, out var chords, out var error))
            {
                return "⚠ " + error;
            }

            return chords.Count == 0
                ? EmptyDescription
                : string.Join(", ", chords.Select(chord => chord.Text));
        }

        /// <summary>
        /// 解析按键序列，遇到无法识别的写法会抛出 <see cref="FormatException"/>。
        /// </summary>
        public static List<KeyChord> Parse(string text)
        {
            var chords = new List<KeyChord>();

            if (string.IsNullOrWhiteSpace(text))
            {
                return chords;
            }

            foreach (var group in Split(text, ChordSeparator))
            {
                if (KeyCodes.Unescape(group).Trim().Length == 0)
                {
                    continue;
                }

                if (TryParseChord(group.Trim(), Split(group, ModifierSeparator), out var chord, out var error))
                {
                    chords.Add(chord);
                    continue;
                }

                // "Down Down Enter"、"Ctrl+A Delete" 这种用空格分开的老写法：每个词各算一个组合
                if (HasInnerWhitespace(group) && TryParseTokenList(group, out var separated))
                {
                    chords.AddRange(separated);
                    continue;
                }

                throw new FormatException(error);
            }

            return chords;
        }

        /// <summary>
        /// 解析失败时不抛异常，直接返回空序列。
        /// </summary>
        public static List<KeyChord> ParseOrDefault(string text)
        {
            return TryParse(text, out var chords, out _) ? chords : new List<KeyChord>();
        }

        /// <summary>
        /// 每个元素各解析一次，任何一个写错就整体返回空，方便调用方判空跳过。
        /// </summary>
        public static List<List<KeyChord>> ParseKeysOrDefault(IReadOnlyList<string> keys)
        {
            var list = new List<List<KeyChord>>();

            if (keys == null)
            {
                return list;
            }

            foreach (var text in keys)
            {
                if (!TryParse(text, out var chords, out _))
                {
                    return new List<List<KeyChord>>();
                }

                list.Add(chords);
            }

            return list;
        }

        /// <summary>
        /// 解析失败时不抛异常，用 <paramref name="error"/> 带回原因。
        /// </summary>
        public static bool TryParse(string text, out List<KeyChord> chords, out string error)
        {
            try
            {
                chords = Parse(text);
                error = null;
                return true;
            }
            catch (FormatException ex)
            {
                chords = new List<KeyChord>();
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 校验配置写法，返回 null 表示合法，否则返回错误说明。
        /// </summary>
        public static string Validate(string text)
        {
            return TryParse(text, out _, out var error) ? null : error;
        }

        /// <summary>
        /// 把编辑框里的原文转成配置要存的形式：一个组合一个元素，键名该转义的都转义好。
        /// </summary>
        public static List<string> ToConfig(string text)
        {
            return Parse(text).Select(chord => chord.Text).ToList();
        }

        /// <summary>
        /// <see cref="ToConfig"/> 的不抛异常版本。
        /// </summary>
        public static bool TryToConfig(string text, out List<string> config, out string error)
        {
            if (!TryParse(text, out var chords, out error))
            {
                config = new List<string>();
                return false;
            }

            config = chords.Select(chord => chord.Text).ToList();
            return true;
        }

        /// <summary>
        /// 把配置里的按键还原成编辑框里的原文，多个组合用逗号隔开。
        /// </summary>
        public static string Format(IReadOnlyList<string> keys)
        {
            if (keys == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // 手改过 JSON，或者是老版本写进去的写法，这里顺手规范化一遍
                var chords = ParseOrDefault(key);
                if (chords.Count == 0)
                {
                    parts.Add(key.Trim());
                }
                else
                {
                    parts.AddRange(chords.Select(chord => chord.Text));
                }
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// 解析失败时不抛异常，用 <paramref name="error"/> 带回原因。
        /// </summary>
        private static bool TryParseChord(
            string group,
            IReadOnlyList<string> parts,
            out KeyChord chord,
            out string error)
        {
            try
            {
                chord = ParseChord(group, parts);
                error = null;
                return true;
            }
            catch (FormatException ex)
            {
                chord = null;
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 把一整段按空格拆成多个组合，只要有一个词解析不了就整体作废。
        /// </summary>
        private static bool TryParseTokenList(string group, out List<KeyChord> chords)
        {
            chords = new List<KeyChord>();

            foreach (var token in group.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryParseChord(token.Trim(), Split(token, ModifierSeparator), out var chord, out _))
                {
                    chords = new List<KeyChord>();
                    return false;
                }

                chords.Add(chord);
            }

            return chords.Count > 0;
        }

        private static KeyChord ParseChord(string group, IReadOnlyList<string> parts)
        {
            var modifiers = new List<ushort>();
            ushort mainKey = 0;

            for (var i = 0; i < parts.Count; i++)
            {
                var part = KeyCodes.Unescape(parts[i].Trim());

                if (part.Length == 0)
                {
                    // "Ctrl++" 这种：中间那个 + 被当成分隔符了，但后面什么都没有
                    throw new FormatException(i == 0
                        ? "+ 前面没有按键；要按加号键本身请写成 \\+"
                        : "+ 后面没有按键；要按加号键本身请写成 \\+");
                }

                if (!KeyCodes.TryParse(part, out var code, out var needsShift))
                {
                    throw new FormatException($"无法识别的按键「{part}」");
                }

                // 上档字符（+ _ { 这些）自己就代表 Shift，不用写出来
                if (needsShift && !modifiers.Contains(KeyCodes.Shift))
                {
                    modifiers.Add(KeyCodes.Shift);
                }

                if (KeyCodes.IsModifier(code))
                {
                    if (!modifiers.Contains(code))
                    {
                        modifiers.Add(code);
                    }

                    continue;
                }

                if (mainKey != 0)
                {
                    throw new FormatException($"「{group}」里有多个主键，一次只能按一个键");
                }

                mainKey = code;
            }

            if (mainKey == 0)
            {
                throw new FormatException($"「{group}」缺少主键");
            }

            return KeyChord.Create(mainKey, modifiers.ToArray());
        }

        /// <summary>
        /// 按分隔符切开，但要跳过被 <c>\</c> 转义掉的那个，切出来的片段保留转义字符。
        /// </summary>
        private static List<string> Split(string text, char separator)
        {
            var parts = new List<string>();
            var current = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == KeyCodes.EscapeChar)
                {
                    if (i + 1 >= text.Length)
                    {
                        throw new FormatException("结尾的 \\ 后面还缺一个要转义的字符");
                    }

                    current.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }

                if (c == separator)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            parts.Add(current.ToString());
            return parts;
        }

        /// <summary>
        /// 片段里除了首尾空白之外还有空白 —— 说明是空格分隔的老写法。
        /// </summary>
        private static bool HasInnerWhitespace(string raw)
        {
            return KeyCodes.Unescape(raw).Trim().Any(char.IsWhiteSpace);
        }
    }
}
