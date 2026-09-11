using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;
using Flow.Launcher.Plugin.FillTextToWindows.Views;

namespace TestDemo
{
    /// <summary>
    /// <see cref="KeyboardLayout"/> 的自检。
    /// <para>
    /// 键盘图的位置是手写的一张表，最怕两件事：坐标写重了/漏了，以及键帽上的键码
    /// <see cref="KeyCodes"/> 里根本没注册——后者会让 <see cref="KeyCodes.GetName"/> 给出
    /// <c>0x0C</c> 这种解析不回来的写法，一存就把用户的按键全清掉。
    /// </para>
    /// <para>运行：<c>dotnet run --project TestDemo -p:WithClipboardTest=true -- --keyboard-test</c></para>
    /// </summary>
    internal static class KeyboardLayoutTest
    {
        private const int NavFirstColumn = 62;

        private const int NumpadFirstColumn = 76;

        private static readonly List<string> Failures = new();

        /// <summary>键盘图上的所有键帽，几个用例共用。</summary>
        private static readonly List<KeyCap> Caps = KeyboardLayout.AllCaps.ToList();

        public static int Run()
        {
            Case("每个键帽都能写进配置、再解析回同一个键", EveryCapRoundTrips);
            Case("修饰键键帽都是真的修饰键", ModifierCapsAreModifiers);
            Case("没有两个键帽占同一个格子", NoOverlappingCells);
            Case("主键区每行都正好铺满", MainRowsFillTheWidth);
            Case("方向键区和小键盘没有漏格的洞", RightBlocksAreFilled);

            Console.WriteLine();
            PrintKeyboard();

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
        /// 键帽 → 配置字符串 → 解析回来，键码和修饰键都不能变。
        /// <para>点击键帽和物理按键走的是同一条路（<see cref="KeyChord.Create"/>），所以这条过了，
        /// 键盘图上点出来的东西就一定能存进配置。</para>
        /// </summary>
        private static void EveryCapRoundTrips()
        {
            foreach (var cap in Caps)
            {
                Assert(KeyCodes.IsKnown(cap.VirtualKey), $"{cap.Label} 的键码 0x{cap.VirtualKey:X2} 没在 KeyCodes 里注册");

                // 修饰键本身不能单独当主键（「Ctrl」这种写法解析不回来），配一个 V 来验
                var chord = cap.Modifier
                    ? KeyChord.Create(KeyCodes.V, cap.VirtualKey)
                    : KeyChord.Create(cap.VirtualKey);

                Assert(
                    KeyParser.TryParse(chord.Text, out var parsed, out var error),
                    $"{cap.Label} 写出来是「{chord.Text}」，解析不回来：{error}");

                Assert(parsed.Count == 1, $"{cap.Label} 写出来是「{chord.Text}」，解析出 {parsed.Count} 个组合，应该只有 1 个");

                var expectedMainKey = cap.Modifier ? KeyCodes.V : cap.VirtualKey;
                Assert(
                    parsed[0].VirtualKey == expectedMainKey,
                    $"{cap.Label}（{chord.Text}）解析回来主键是 0x{parsed[0].VirtualKey:X2}，应该是 0x{expectedMainKey:X2}");

                if (cap.Modifier)
                {
                    Assert(
                        parsed[0].Modifiers.Contains(cap.VirtualKey),
                        $"{cap.Label}（{chord.Text}）解析回来修饰键里没有它");
                }
            }
        }

        private static void ModifierCapsAreModifiers()
        {
            var modifierCaps = Caps.Where(cap => cap.Modifier).ToList();
            Assert(modifierCaps.Count > 0, "一个修饰键键帽都没有，键盘图不对");

            foreach (var cap in modifierCaps)
            {
                Assert(
                    KeyCodes.IsModifier(cap.VirtualKey),
                    $"{cap.Label} 标成了修饰键，但 0x{cap.VirtualKey:X2} 在 KeyCodes 里不是修饰键");
            }

            // 左右两边各一个 Ctrl/Shift/Alt/Win，点哪个都该是同一个键码
            foreach (var label in new[] { "Ctrl", "Shift", "Alt", "Win" })
            {
                var codes = Caps.Where(cap => cap.Label == label).Select(cap => cap.VirtualKey).Distinct().ToList();
                Assert(codes.Count == 1, $"键盘图上有 {codes.Count} 种「{label}」键码，应该只有 1 种（左右不分）");
            }
        }

        private static void NoOverlappingCells()
        {
            var used = new Dictionary<(int Row, int Column), string>();

            foreach (var cap in Caps)
            {
                Assert(cap.ColumnSpan > 0 && cap.RowSpan > 0, $"{cap.Label} 的格子数是 0");
                Assert(cap.Column >= 0 && cap.Row >= 0, $"{cap.Label} 的坐标是负的");
                Assert(
                    cap.Column + cap.ColumnSpan <= KeyboardLayout.Columns,
                    $"{cap.Label} 超出了键盘右边（第 {cap.Column + cap.ColumnSpan} 格 > {KeyboardLayout.Columns}）");
                Assert(
                    cap.Row + cap.RowSpan <= KeyboardLayout.Rows,
                    $"{cap.Label} 超出了键盘下边（第 {cap.Row + cap.RowSpan} 行 > {KeyboardLayout.Rows}）");
                Assert(!string.IsNullOrWhiteSpace(cap.Label), $"第 {cap.Row} 行第 {cap.Column} 格没有标签");

                for (var row = cap.Row; row < cap.Row + cap.RowSpan; row++)
                {
                    for (var column = cap.Column; column < cap.Column + cap.ColumnSpan; column++)
                    {
                        var cell = (row, column);
                        Assert(
                            !used.ContainsKey(cell),
                            $"第 {row} 行第 {column} 格被「{used.GetValueOrDefault(cell)}」和「{cap.Label}」重叠占了");

                        used[cell] = cap.Label;
                    }
                }
            }
        }

        private static void MainRowsFillTheWidth()
        {
            for (var row = 1; row < KeyboardLayout.Rows; row++)
            {
                var caps = Caps
                    .Where(cap => cap.Row == row && cap.Column < KeyboardLayout.MainColumns)
                    .OrderBy(cap => cap.Column)
                    .ToList();

                Assert(caps.Count > 0, $"主键区第 {row} 行一个键都没有");

                var cursor = 0;
                foreach (var cap in caps)
                {
                    Assert(
                        cap.Column == cursor,
                        $"主键区第 {row} 行在第 {cursor} 格缺了/多了一块（下一个键「{cap.Label}」从第 {cap.Column} 格开始）");

                    cursor = cap.Column + cap.ColumnSpan;
                }

                Assert(cursor == KeyboardLayout.MainColumns, $"主键区第 {row} 行宽度是 {cursor} 格，应该是 {KeyboardLayout.MainColumns}");
            }
        }

        private static void RightBlocksAreFilled()
        {
            // 小键盘 4 列 × 5 行，一格都不能漏（+ 和回车跨两行，正好补上）
            var used = new HashSet<(int Row, int Column)>();
            foreach (var cap in Caps)
            {
                for (var row = cap.Row; row < cap.Row + cap.RowSpan; row++)
                {
                    for (var column = cap.Column; column < cap.Column + cap.ColumnSpan; column++)
                    {
                        used.Add((row, column));
                    }
                }
            }

            for (var row = 1; row < KeyboardLayout.Rows; row++)
            {
                for (var column = NumpadFirstColumn; column < KeyboardLayout.Columns; column++)
                {
                    Assert(used.Contains((row, column)), $"小键盘第 {row} 行第 {column} 格是空的");
                }
            }

            // 方向键区上面 4 个键 + 方向键都得在
            foreach (var label in new[] { "Ins", "Home", "PgUp", "Del", "End", "PgDn", "↑", "←", "↓", "→" })
            {
                Assert(Caps.Any(cap => cap.Label == label && cap.Column >= NavFirstColumn), $"方向键区少了「{label}」");
            }
        }

        /// <summary>把键盘图按行打出来，肉眼看一眼形状：<c>·</c> 表示那几格是空的。</summary>
        private static void PrintKeyboard()
        {
            for (var row = 0; row < KeyboardLayout.Rows; row++)
            {
                Console.WriteLine($"第 {row} 行  {DescribeRow(row, 0, KeyboardLayout.MainColumns)}");
            }

            for (var row = 1; row < KeyboardLayout.Rows; row++)
            {
                Console.WriteLine($"第 {row} 行  {DescribeRow(row, NavFirstColumn, KeyboardLayout.Columns)}");
            }
        }

        private static string DescribeRow(int row, int firstColumn, int endColumn)
        {
            var builder = new StringBuilder();
            var cursor = firstColumn;

            foreach (var cap in Caps.Where(cap => cap.Row == row && cap.Column >= firstColumn && cap.Column < endColumn)
                         .OrderBy(cap => cap.Column))
            {
                if (cap.Column > cursor)
                {
                    builder.Append('·');
                }
                else if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(cap.Label).Append('(').Append(cap.ColumnSpan).Append(')');
                cursor = cap.Column + cap.ColumnSpan;
            }

            builder.Append($"   → 到第 {cursor} 格");
            return builder.ToString();
        }

        private static void Case(string name, Action body)
        {
            try
            {
                body();
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
    }
}
