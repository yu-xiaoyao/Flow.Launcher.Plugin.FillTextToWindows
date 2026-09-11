using System;
using System.Collections.Generic;
using System.Text;

namespace Flow.Launcher.Plugin.FillTextToWindows.Keys
{
    /// <summary>
    /// 按键名 ↔ 虚拟键码。名字不区分大小写，覆盖整个键盘：字母数字、F1~F24、小键盘、
    /// 标点符号、修饰键，以及多媒体 / 浏览器键。
    /// <para>
    /// 标点键按「在美式键盘上打出来的那个字符」命名：<c>,</c> <c>=</c> <c>-</c> 这类不用按 Shift，
    /// <c>+</c> <c>_</c> <c>{</c> 这类本来就要按着 Shift 才打得出来，解析时会自动把 Shift 带上。
    /// </para>
    /// </summary>
    internal static class KeyCodes
    {
        // ---- 修饰键 ----
        public const ushort Shift = 0x10;
        public const ushort Control = 0x11;
        public const ushort Alt = 0x12;
        public const ushort LeftShift = 0xA0;
        public const ushort RightShift = 0xA1;
        public const ushort LeftControl = 0xA2;
        public const ushort RightControl = 0xA3;
        public const ushort LeftAlt = 0xA4;
        public const ushort RightAlt = 0xA5;
        public const ushort LeftWindows = 0x5B;
        public const ushort RightWindows = 0x5C;

        // ---- 编辑 / 导航 ----
        public const ushort Backspace = 0x08;
        public const ushort Tab = 0x09;
        public const ushort Enter = 0x0D;
        public const ushort Pause = 0x13;
        public const ushort CapsLock = 0x14;
        public const ushort Escape = 0x1B;
        public const ushort Space = 0x20;
        public const ushort PageUp = 0x21;
        public const ushort PageDown = 0x22;
        public const ushort End = 0x23;
        public const ushort Home = 0x24;
        public const ushort Left = 0x25;
        public const ushort Up = 0x26;
        public const ushort Right = 0x27;
        public const ushort Down = 0x28;
        public const ushort PrintScreen = 0x2C;
        public const ushort Insert = 0x2D;
        public const ushort Delete = 0x2E;
        public const ushort Help = 0x2F;
        public const ushort Apps = 0x5D;
        public const ushort Sleep = 0x5F;
        public const ushort NumLock = 0x90;
        public const ushort ScrollLock = 0x91;

        // ---- 小键盘 ----
        public const ushort NumPad0 = 0x60;
        public const ushort Multiply = 0x6A;
        public const ushort Add = 0x6B;
        public const ushort Separator = 0x6C;
        public const ushort Subtract = 0x6D;
        public const ushort Decimal = 0x6E;
        public const ushort Divide = 0x6F;

        // ---- 标点，名字就是美式键盘上不按 Shift 打出来的那个字符 ----
        public const ushort Oem1 = 0xBA;      // ; :
        public const ushort OemPlus = 0xBB;   // = +
        public const ushort OemComma = 0xBC;  // , <
        public const ushort OemMinus = 0xBD;  // - _
        public const ushort OemPeriod = 0xBE; // . >
        public const ushort Oem2 = 0xBF;      // / ?
        public const ushort Oem3 = 0xC0;      // ` ~
        public const ushort Oem4 = 0xDB;      // [ {
        public const ushort Oem5 = 0xDC;      // \ |
        public const ushort Oem6 = 0xDD;      // ] }
        public const ushort Oem7 = 0xDE;      // ' "

        // ---- 浏览器 / 多媒体 ----
        public const ushort BrowserBack = 0xA6;
        public const ushort BrowserForward = 0xA7;
        public const ushort BrowserRefresh = 0xA8;
        public const ushort BrowserStop = 0xA9;
        public const ushort BrowserSearch = 0xAA;
        public const ushort BrowserFavorites = 0xAB;
        public const ushort BrowserHome = 0xAC;
        public const ushort VolumeMute = 0xAD;
        public const ushort VolumeDown = 0xAE;
        public const ushort VolumeUp = 0xAF;
        public const ushort MediaNext = 0xB0;
        public const ushort MediaPrev = 0xB1;
        public const ushort MediaStop = 0xB2;
        public const ushort MediaPlay = 0xB3;
        public const ushort LaunchMail = 0xB4;
        public const ushort LaunchMedia = 0xB5;
        public const ushort LaunchApp1 = 0xB6;
        public const ushort LaunchApp2 = 0xB7;

        public const ushort A = 0x41;
        public const ushort V = 0x56;

        /// <summary>F1 ~ F24。</summary>
        public const ushort F1 = 0x70;

        // ---- 解析 -- 拼写 ----

        /// <summary>转义字符：<c>\+</c> 表示加号键本身。</summary>
        public const char EscapeChar = '\\';

        /// <summary>需要转义才能当成按键名写的字符。</summary>
        private const string SpecialChars = "+,\\";

        private static readonly Dictionary<string, KeyName> Names = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<ushort, KeyDefinition> Codes = new();

        static KeyCodes()
        {
            foreach (var definition in BuildDefinitions())
            {
                Codes[definition.Code] = definition;

                Register(definition.Name, definition, needsShift: false);

                if (definition.Aliases != null)
                {
                    foreach (var alias in definition.Aliases)
                    {
                        Register(alias, definition, needsShift: false);
                    }
                }

                if (definition.ShiftedName != null)
                {
                    Register(definition.ShiftedName, definition, needsShift: true);
                }

                if (definition.ShiftedAliases != null)
                {
                    foreach (var alias in definition.ShiftedAliases)
                    {
                        Register(alias, definition, needsShift: true);
                    }
                }
            }
        }

        /// <summary>
        /// 判断一个键码是不是修饰键。
        /// </summary>
        public static bool IsModifier(ushort code)
        {
            return Codes.TryGetValue(code, out var definition) && definition.Modifier;
        }

        /// <summary>
        /// 判断这个键码认不认识。
        /// <para>
        /// <see cref="GetName"/> 对不认识的键码会返回 <c>0xNN</c> 这种写法，而那个字符串是解析不回来的
        /// （<see cref="TryParse"/> 只查名字表），写进配置等于把这一串按键全清掉。
        /// 录制这类「先拿到键码再写字」的用法，要先用这个问一下。
        /// </para>
        /// </summary>
        public static bool IsKnown(ushort code)
        {
            return Codes.ContainsKey(code);
        }

        /// <summary>
        /// 按键名解析键码。<paramref name="needsShift"/> 表示这个名字本来就要按住 Shift 才打得出来
        /// （例如 <c>+</c>、<c>_</c>、<c>{</c>），调用方要自己把 Shift 补进修饰键里。
        /// </summary>
        public static bool TryParse(string name, out ushort code, out bool needsShift)
        {
            code = 0;
            needsShift = false;

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (!Names.TryGetValue(name.Trim(), out var found))
            {
                return false;
            }

            code = found.Definition.Code;
            needsShift = found.NeedsShift;
            return true;
        }

        /// <summary>
        /// 把键码转成规范名。<paramref name="withShift"/> 为 true 且这个键上档有字符时返回上档字符
        /// （<c>=</c> 会变成 <c>+</c>），这样 <c>Ctrl+Shift+=</c> 写出来就是 <c>Ctrl+\+</c>。
        /// </summary>
        public static string GetName(ushort code, bool withShift = false)
        {
            if (Codes.TryGetValue(code, out var definition))
            {
                return withShift && definition.ShiftedName != null ? definition.ShiftedName : definition.Name;
            }

            return "0x" + code.ToString("X2");
        }

        /// <summary>
        /// 把按键名转成能写进配置的形态：名字里出现 <c>+</c>、<c>,</c>、<c>\</c> 时前面补一个 <c>\</c>。
        /// </summary>
        public static string EscapeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length + 2);

            foreach (var c in name)
            {
                if (SpecialChars.IndexOf(c) >= 0)
                {
                    builder.Append(EscapeChar);
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 去掉按键名里的转义字符，<c>\+</c> → <c>+</c>。
        /// </summary>
        public static string Unescape(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(EscapeChar) < 0)
            {
                return text ?? string.Empty;
            }

            var builder = new StringBuilder(text.Length);

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == EscapeChar && i + 1 < text.Length)
                {
                    i++;
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private static void Register(string name, KeyDefinition definition, bool needsShift)
        {
            // 同一个名字不允许指向两个键，真撞上了说明表里写重了
            Names[name] = new KeyName(definition, needsShift);
        }

        private static IEnumerable<KeyDefinition> BuildDefinitions()
        {
            // 修饰键
            yield return new KeyDefinition("Ctrl", Control, modifier: true, aliases: new[] { "Control" });
            yield return new KeyDefinition("LCtrl", LeftControl, modifier: true, aliases: new[] { "LeftCtrl" });
            yield return new KeyDefinition("RCtrl", RightControl, modifier: true, aliases: new[] { "RightCtrl" });
            yield return new KeyDefinition("Shift", Shift, modifier: true);
            yield return new KeyDefinition("LShift", LeftShift, modifier: true, aliases: new[] { "LeftShift" });
            yield return new KeyDefinition("RShift", RightShift, modifier: true, aliases: new[] { "RightShift" });
            yield return new KeyDefinition("Alt", Alt, modifier: true, aliases: new[] { "Menu" });
            yield return new KeyDefinition("LAlt", LeftAlt, modifier: true, aliases: new[] { "LeftAlt" });
            yield return new KeyDefinition("RAlt", RightAlt, modifier: true, aliases: new[] { "RightAlt", "AltGr" });
            yield return new KeyDefinition("Win", LeftWindows, modifier: true,
                aliases: new[] { "Windows", "Super", "Meta", "LWin", "LeftWin" });
            yield return new KeyDefinition("RWin", RightWindows, modifier: true, aliases: new[] { "RightWin" });

            // 编辑 / 导航
            yield return new KeyDefinition("Backspace", Backspace, aliases: new[] { "Back" });
            yield return new KeyDefinition("Tab", Tab);
            // 小键盘回车在 VK 上跟主回车是同一个键，这里不带 EXTENDEDKEY 标志，发出去的是主回车
            yield return new KeyDefinition("Enter", Enter, aliases: new[] { "Return", "NumPadEnter" });
            yield return new KeyDefinition("Esc", Escape, aliases: new[] { "Escape" });
            yield return new KeyDefinition("Space", Space, aliases: new[] { "Spacebar" });
            yield return new KeyDefinition("CapsLock", CapsLock);
            yield return new KeyDefinition("NumLock", NumLock);
            yield return new KeyDefinition("ScrollLock", ScrollLock);
            yield return new KeyDefinition("Pause", Pause, aliases: new[] { "Break" });
            yield return new KeyDefinition("PrintScreen", PrintScreen,
                aliases: new[] { "PrtSc", "PrtScn", "Snapshot" });
            yield return new KeyDefinition("Insert", Insert, aliases: new[] { "Ins" });
            yield return new KeyDefinition("Delete", Delete, aliases: new[] { "Del" });
            yield return new KeyDefinition("Home", Home);
            yield return new KeyDefinition("End", End);
            yield return new KeyDefinition("PageUp", PageUp, aliases: new[] { "PgUp" });
            yield return new KeyDefinition("PageDown", PageDown, aliases: new[] { "PgDn" });
            yield return new KeyDefinition("Left", Left);
            yield return new KeyDefinition("Right", Right);
            yield return new KeyDefinition("Up", Up);
            yield return new KeyDefinition("Down", Down);
            yield return new KeyDefinition("Apps", Apps, aliases: new[] { "Application" });
            yield return new KeyDefinition("Help", Help);
            yield return new KeyDefinition("Sleep", Sleep);

            // 小键盘
            for (var i = 0; i <= 9; i++)
            {
                yield return new KeyDefinition("Num" + i, (ushort)(NumPad0 + i), aliases: new[] { "NumPad" + i });
            }

            yield return new KeyDefinition("NumMultiply", Multiply, aliases: new[] { "Multiply", "NumPadMultiply" });
            yield return new KeyDefinition("NumAdd", Add, aliases: new[] { "Add", "NumPadAdd", "NumPadPlus" });
            yield return new KeyDefinition("NumSeparator", Separator, aliases: new[] { "Separator" });
            yield return new KeyDefinition("NumSubtract", Subtract,
                aliases: new[] { "Subtract", "NumPadSubtract", "NumPadMinus" });
            yield return new KeyDefinition("NumDecimal", Decimal,
                aliases: new[] { "Decimal", "NumPadDecimal", "NumPadDot" });
            yield return new KeyDefinition("NumDivide", Divide, aliases: new[] { "Divide", "NumPadDivide" });

            // 字母数字
            for (var c = 'A'; c <= 'Z'; c++)
            {
                yield return new KeyDefinition(c.ToString(), c);
            }

            yield return new KeyDefinition("0", 0x30, shiftedName: ")");
            yield return new KeyDefinition("1", 0x31, shiftedName: "!");
            yield return new KeyDefinition("2", 0x32, shiftedName: "@");
            yield return new KeyDefinition("3", 0x33, shiftedName: "#");
            yield return new KeyDefinition("4", 0x34, shiftedName: "$");
            yield return new KeyDefinition("5", 0x35, shiftedName: "%");
            yield return new KeyDefinition("6", 0x36, shiftedName: "^");
            yield return new KeyDefinition("7", 0x37, shiftedName: "&");
            yield return new KeyDefinition("8", 0x38, shiftedName: "*", shiftedAliases: new[] { "Asterisk" });
            yield return new KeyDefinition("9", 0x39, shiftedName: "(");

            // 标点
            yield return new KeyDefinition("`", Oem3, aliases: new[] { "Backquote", "Grave" },
                shiftedName: "~", shiftedAliases: new[] { "Tilde" });
            yield return new KeyDefinition("-", OemMinus, aliases: new[] { "Minus" },
                shiftedName: "_", shiftedAliases: new[] { "Underscore" });
            yield return new KeyDefinition("=", OemPlus, aliases: new[] { "Equals" },
                shiftedName: "+", shiftedAliases: new[] { "Plus" });
            yield return new KeyDefinition("[", Oem4, aliases: new[] { "LeftBracket" },
                shiftedName: "{", shiftedAliases: new[] { "LeftBrace" });
            yield return new KeyDefinition("]", Oem6, aliases: new[] { "RightBracket" },
                shiftedName: "}", shiftedAliases: new[] { "RightBrace" });
            yield return new KeyDefinition("\\", Oem5, aliases: new[] { "Backslash" },
                shiftedName: "|", shiftedAliases: new[] { "Pipe", "Bar" });
            yield return new KeyDefinition(";", Oem1, aliases: new[] { "Semicolon" },
                shiftedName: ":", shiftedAliases: new[] { "Colon" });
            yield return new KeyDefinition("'", Oem7, aliases: new[] { "Quote", "Apostrophe" },
                shiftedName: "\"", shiftedAliases: new[] { "DoubleQuote" });
            yield return new KeyDefinition(",", OemComma, aliases: new[] { "Comma" },
                shiftedName: "<", shiftedAliases: new[] { "Less" });
            yield return new KeyDefinition(".", OemPeriod, aliases: new[] { "Period", "Dot" },
                shiftedName: ">", shiftedAliases: new[] { "Greater" });
            yield return new KeyDefinition("/", Oem2, aliases: new[] { "Slash" },
                shiftedName: "?", shiftedAliases: new[] { "Question" });

            // 浏览器 / 多媒体
            yield return new KeyDefinition("BrowserBack", BrowserBack);
            yield return new KeyDefinition("BrowserForward", BrowserForward);
            yield return new KeyDefinition("BrowserRefresh", BrowserRefresh);
            yield return new KeyDefinition("BrowserStop", BrowserStop);
            yield return new KeyDefinition("BrowserSearch", BrowserSearch);
            yield return new KeyDefinition("BrowserFavorites", BrowserFavorites);
            yield return new KeyDefinition("BrowserHome", BrowserHome);
            yield return new KeyDefinition("VolumeMute", VolumeMute);
            yield return new KeyDefinition("VolumeDown", VolumeDown);
            yield return new KeyDefinition("VolumeUp", VolumeUp);
            yield return new KeyDefinition("MediaNext", MediaNext);
            yield return new KeyDefinition("MediaPrev", MediaPrev);
            yield return new KeyDefinition("MediaStop", MediaStop);
            yield return new KeyDefinition("MediaPlay", MediaPlay);
            yield return new KeyDefinition("LaunchMail", LaunchMail);
            yield return new KeyDefinition("LaunchMedia", LaunchMedia);
            yield return new KeyDefinition("LaunchApp1", LaunchApp1);
            yield return new KeyDefinition("LaunchApp2", LaunchApp2);

            // F1 ~ F24
            for (var i = 1; i <= 24; i++)
            {
                yield return new KeyDefinition("F" + i, (ushort)(F1 + i - 1));
            }
        }

        private readonly struct KeyName
        {
            public KeyName(KeyDefinition definition, bool needsShift)
            {
                Definition = definition;
                NeedsShift = needsShift;
            }

            public KeyDefinition Definition { get; }

            public bool NeedsShift { get; }
        }

        private sealed class KeyDefinition
        {
            public KeyDefinition(
                string name,
                ushort code,
                bool modifier = false,
                string[] aliases = null,
                string shiftedName = null,
                string[] shiftedAliases = null)
            {
                Name = name;
                Code = code;
                Modifier = modifier;
                Aliases = aliases;
                ShiftedName = shiftedName;
                ShiftedAliases = shiftedAliases;
            }

            /// <summary>规范名，写进配置的就是它。</summary>
            public string Name { get; }

            public ushort Code { get; }

            public bool Modifier { get; }

            public string[] Aliases { get; }

            /// <summary>按住 Shift 打出来的那个字符，没有就是 null。</summary>
            public string ShiftedName { get; }

            public string[] ShiftedAliases { get; }
        }
    }
}
