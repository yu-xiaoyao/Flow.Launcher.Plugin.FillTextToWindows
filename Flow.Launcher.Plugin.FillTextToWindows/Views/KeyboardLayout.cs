using System.Collections.Generic;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    /// <summary>
    /// 键盘图上的一颗键帽。
    /// <para>
    /// 位置和大小都用「格子」表示，一个字母键宽 = <see cref="KeyboardLayout.KeyUnit"/> 格，
    /// 整个键盘宽 <see cref="KeyboardLayout.Columns"/> 格、高 <see cref="KeyboardLayout.Rows"/> 行。
    /// </para>
    /// </summary>
    public readonly struct KeyCap
    {
        public KeyCap(
            ushort virtualKey,
            string label,
            int column,
            int row,
            int columnSpan,
            int rowSpan,
            bool modifier)
        {
            VirtualKey = virtualKey;
            Label = label;
            Column = column;
            Row = row;
            ColumnSpan = columnSpan;
            RowSpan = rowSpan;
            Modifier = modifier;
        }

        /// <summary>按下去之后要发给 <see cref="KeyChord"/> 的键码。</summary>
        public ushort VirtualKey { get; }

        /// <summary>键帽上显示的字。为了放得下，比 <see cref="KeyCodes.GetName"/> 的名字短。</summary>
        public string Label { get; }

        /// <summary>左起第几格。</summary>
        public int Column { get; }

        /// <summary>第几行（0 是功能键行）。</summary>
        public int Row { get; }

        /// <summary>宽几格。</summary>
        public int ColumnSpan { get; }

        /// <summary>高几行，小键盘的 <c>+</c> 和回车是 2。</summary>
        public int RowSpan { get; }

        /// <summary>是不是修饰键。点它是「按住」，不是「按一下」。</summary>
        public bool Modifier { get; }
    }

    /// <summary>
    /// 一张标准 104 键键盘的位置表，给录制对话框画键盘图用。
    /// <para>
    /// 纯数据，不碰 WPF：坐标全在 <see cref="AllCaps"/> 里写死，界面那边按格子铺一遍就行，
    /// 测试那边可以直接验「每行都铺满、没有重叠」（见 TestDemo/KeyboardLayoutTest.cs）。
    /// </para>
    /// </summary>
    public static class KeyboardLayout
    {
        /// <summary>一个字母键占的格子数，其它宽度都是它的倍数（Tab 1.5 键 = 6 格）。</summary>
        public const int KeyUnit = 4;

        /// <summary>主键区宽多少格（字母键 × 15）。</summary>
        public const int MainColumns = 60;

        /// <summary>整张图宽多少格：主键区 + 间隔 + 方向键区 + 间隔 + 小键盘。</summary>
        public const int Columns = 92;

        /// <summary>整张图多少行：功能键行 + 主键区 5 行。</summary>
        public const int Rows = 6;

        /// <summary>所有键帽。</summary>
        public static IReadOnlyList<KeyCap> AllCaps { get; } = BuildAll();

        private static List<KeyCap> BuildAll()
        {
            var caps = new List<KeyCap>();

            BuildFunctionRow(caps);
            BuildNumberRow(caps);
            BuildQwertyRow(caps);
            BuildHomeRow(caps);
            BuildLetterRow(caps);
            BuildModifierRow(caps);
            BuildNavigationBlock(caps);
            BuildNumpadBlock(caps);

            return caps;
        }

        /// <summary>
        /// 功能键行：Esc、F1～F12（每 4 个一组）、PrtSc / ScrLk / Pause。
        /// <para>F 键只有 3 格宽，不然这一行放不下 15 个字母键的宽度。</para>
        /// </summary>
        private static void BuildFunctionRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 0);

            row.Key(KeyCodes.Escape, "Esc").Gap();
            row.FunctionKeys(1).Gap().FunctionKeys(5).Gap().FunctionKeys(9).Gap();
            row.Key(KeyCodes.PrintScreen, "PrtSc")
                .Key(KeyCodes.ScrollLock, "ScrLk")
                .Key(KeyCodes.Pause, "Pause");
        }

        /// <summary>` 1～9 0 - = Backspace。</summary>
        private static void BuildNumberRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 1);

            row.Key(KeyCodes.Oem3, "`");

            for (var digit = 1; digit <= 9; digit++)
            {
                row.Key((ushort)(0x30 + digit), digit.ToString());
            }

            row.Key(0x30, "0")
                .Key(KeyCodes.OemMinus, "-")
                .Key(KeyCodes.OemPlus, "=")
                .Key(KeyCodes.Backspace, "Backspace", KeyUnit * 2);
        }

        /// <summary>Tab Q～P [ ] \。</summary>
        private static void BuildQwertyRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 2);

            row.Key(KeyCodes.Tab, "Tab", KeyUnit * 3 / 2);
            row.Letters("QWERTYUIOP");
            row.Key(KeyCodes.Oem4, "[")
                .Key(KeyCodes.Oem6, "]")
                .Key(KeyCodes.Oem5, "\\", KeyUnit * 3 / 2);
        }

        /// <summary>Caps A～L ; ' Enter。</summary>
        private static void BuildHomeRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 3);

            row.Key(KeyCodes.CapsLock, "Caps", 7);   // 1.75 键，格子数只能是整数，取 7
            row.Letters("ASDFGHJKL");
            row.Key(KeyCodes.Oem1, ";")
                .Key(KeyCodes.Oem7, "'")
                .Key(KeyCodes.Enter, "Enter", 9);    // 2.25 键
        }

        /// <summary>Shift Z～M , . / Shift。左右两个 Shift 都是同一个修饰键。</summary>
        private static void BuildLetterRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 4);

            row.Key(KeyCodes.Shift, "Shift", 9, modifier: true);
            row.Letters("ZXCVBNM");
            row.Key(KeyCodes.OemComma, ",")
                .Key(KeyCodes.OemPeriod, ".")
                .Key(KeyCodes.Oem2, "/")
                .Key(KeyCodes.Shift, "Shift", 11, modifier: true);
        }

        /// <summary>
        /// 最下面一行。修饰键统一用不分左右的键码（<see cref="KeyCodes.Control"/> 而不是
        /// <see cref="KeyCodes.LeftControl"/>）：混着用的话 <see cref="KeyChord.Text"/> 会写成
        /// <c>Ctrl+LCtrl+V</c>，存进配置里没法看。
        /// </summary>
        private static void BuildModifierRow(List<KeyCap> caps)
        {
            var row = new Row(caps, 5);

            row.Key(KeyCodes.Control, "Ctrl", 5, modifier: true)
                .Key(KeyCodes.LeftWindows, "Win", 5, modifier: true)
                .Key(KeyCodes.Alt, "Alt", 5, modifier: true)
                .Key(KeyCodes.Space, "Space", 25)
                .Key(KeyCodes.Alt, "Alt", 5, modifier: true)
                .Key(KeyCodes.LeftWindows, "Win", 5, modifier: true)
                .Key(KeyCodes.Apps, "Menu", 5)
                .Key(KeyCodes.Control, "Ctrl", 5, modifier: true);
        }

        /// <summary>方向键区上面那 6 个键，再往下是方向键。</summary>
        private static void BuildNavigationBlock(List<KeyCap> caps)
        {
            new Row(caps, 1, 62).Key(KeyCodes.Insert, "Ins")
                .Key(KeyCodes.Home, "Home")
                .Key(KeyCodes.PageUp, "PgUp");

            new Row(caps, 2, 62).Key(KeyCodes.Delete, "Del")
                .Key(KeyCodes.End, "End")
                .Key(KeyCodes.PageDown, "PgDn");

            new Row(caps, 3, 66).Key(KeyCodes.Up, "↑");

            new Row(caps, 4, 62).Key(KeyCodes.Left, "←")
                .Key(KeyCodes.Down, "↓")
                .Key(KeyCodes.Right, "→");
        }

        /// <summary>
        /// 小键盘。<c>+</c> 和回车各占两行，0 占两格，跟真键盘一样。
        /// <para>小键盘回车在 VK 上跟主回车是同一个键，这里就按 <see cref="KeyCodes.Enter"/> 记，
        /// 和 <see cref="KeyCodes"/> 里已有的说明一致。</para>
        /// </summary>
        private static void BuildNumpadBlock(List<KeyCap> caps)
        {
            new Row(caps, 1, 76).Key(KeyCodes.NumLock, "Num")
                .Key(KeyCodes.Divide, "/")
                .Key(KeyCodes.Multiply, "*")
                .Key(KeyCodes.Subtract, "-");

            new Row(caps, 2, 76).Key(KeyCodes.NumPad0 + 7, "7")
                .Key(KeyCodes.NumPad0 + 8, "8")
                .Key(KeyCodes.NumPad0 + 9, "9")
                .Key(KeyCodes.Add, "+", rowSpan: 2);

            new Row(caps, 3, 76).Key(KeyCodes.NumPad0 + 4, "4")
                .Key(KeyCodes.NumPad0 + 5, "5")
                .Key(KeyCodes.NumPad0 + 6, "6");

            new Row(caps, 4, 76).Key(KeyCodes.NumPad0 + 1, "1")
                .Key(KeyCodes.NumPad0 + 2, "2")
                .Key(KeyCodes.NumPad0 + 3, "3")
                .Key(KeyCodes.Enter, "Enter", rowSpan: 2);

            new Row(caps, 5, 76).Key(KeyCodes.NumPad0, "0", KeyUnit * 2)
                .Key(KeyCodes.Decimal, ".");
        }

        /// <summary>
        /// 一行键帽的游标：<see cref="Key"/> 之后游标自己往后走，<see cref="Gap"/> 空出一段，
        /// 右边的方向键区、小键盘不在第 0 格开头，从构造函数里指定起始格。
        /// </summary>
        private sealed class Row
        {
            private readonly List<KeyCap> _caps;

            private readonly int _row;

            private int _column;

            public Row(List<KeyCap> caps, int row, int column = 0)
            {
                _caps = caps;
                _row = row;
                _column = column;
            }

            public Row Key(ushort virtualKey, string label, int span = KeyUnit, int rowSpan = 1, bool modifier = false)
            {
                _caps.Add(new KeyCap(virtualKey, label, _column, _row, span, rowSpan, modifier));
                _column += span;
                return this;
            }

            /// <summary>
            /// 按顺序放一排字母键。字母在键盘上不是连着排的（QWERTY 那行是 Q W E R T Y U I O P），
            /// 所以要把顺序写出来，别拿 <c>'Q'</c> 往后加。
            /// </summary>
            public Row Letters(string letters)
            {
                foreach (var letter in letters)
                {
                    Key(letter, letter.ToString());
                }

                return this;
            }

            /// <summary>F 键只有 3 格宽，单独一个入口写清楚点。</summary>
            public Row FunctionKeys(int first)
            {
                for (var i = 0; i < 4; i++)
                {
                    var number = first + i;
                    Key((ushort)(KeyCodes.F1 + number - 1), "F" + number, 3);
                }

                return this;
            }

            /// <summary>空一段。</summary>
            public Row Gap(int span = 2)
            {
                _column += span;
                return this;
            }
        }
    }
}
