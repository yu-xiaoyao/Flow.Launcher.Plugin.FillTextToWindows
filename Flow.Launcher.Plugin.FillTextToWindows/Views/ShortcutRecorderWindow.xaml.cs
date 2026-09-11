using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Plugin.FillTextToWindows.Interop;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;
using Flow.Launcher.Plugin.FillTextToWindows.ViewModels;

namespace Flow.Launcher.Plugin.FillTextToWindows.Views
{
    /// <summary>
    /// 录制按键的对话框：上面是录到的结果，下面是键盘图，按物理键或者点键帽都能录。
    /// <para>
    /// 从「录制」按钮打开（<see cref="Record"/>），点保存之后结果会被写回那个输入框。
    /// 键盘图的样子在 <see cref="KeyboardLayout"/> 里，这里是把它铺出来、接事件。
    /// </para>
    /// </summary>
    public partial class ShortcutRecorderWindow : Window
    {
        /// <summary>一个格子的宽度，<see cref="KeyboardLayout.KeyUnit"/> 格 = 一个字母键。</summary>
        private const double CellSize = 11.6;

        /// <summary>一行的行距，比键帽高一点点，留出键缝。</summary>
        private const double RowPitch = 47;

        private const double CapGapX = 4;

        private const double CapGapY = 5;

        /// <summary>键帽宽到这个数就用大一号的字（Backspace 这种长名字放得下）。</summary>
        private const double WideCapWidth = 60;

        /// <summary>VK_CLEAR：NumLock 关着的时候按小键盘 5。键名表里没有它，<see cref="KeyCodes"/> 里也就没有常量。</summary>
        private const ushort VirtualKeyClear = 0x0C;

        private readonly ShortcutRecorderViewModel _viewModel;

        /// <summary>键盘图上的键帽，按（归一化过的）键码分组，用来点亮。</summary>
        private readonly Dictionary<ushort, List<Button>> _capsByKeyCode = new();

        /// <summary>物理按住的键，只用来点亮键帽。</summary>
        private readonly List<ushort> _heldKeys = new();

        /// <summary>点键帽点出来的修饰键，录下一条之后清掉。</summary>
        private readonly List<ushort> _pendingModifiers = new();

        private readonly Style _capStyle;

        private readonly Style _capOnStyle;

        public ShortcutRecorderWindow(ShortcutRecorderViewModel viewModel, string label)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            // 三个按键设置用的是同一个对话框，标题里带上录的是哪一个
            if (!string.IsNullOrWhiteSpace(label))
            {
                Title = Title + " —— " + label;
            }

            _capStyle = (Style)FindResource("KeyCapStyle");
            _capOnStyle = (Style)FindResource("KeyCapOnStyle");

            BuildKeyboard();
        }

        /// <summary>点过「保存」没有。窗口关掉之后看这个决定要不要写回输入框。</summary>
        public bool Saved { get; private set; }

        /// <summary>录到的按键，只有 <see cref="Saved"/> 为 true 才有意义。</summary>
        public IReadOnlyList<string> Result => _viewModel.Result;

        /// <summary>
        /// 打开录制对话框，点了保存就把结果写回输入框。
        /// <para>
        /// <paramref name="owner"/> 传「录制」按钮所在的那个窗口（<c>Window.GetWindow(this)</c>），
        /// 对话框会跟着它居中、也一直盖在它上面。别传 Flow Launcher 的主窗口：它会被隐藏，
        /// 跟着它一起藏起来的对话框就成了看不见的模态窗口。
        /// </para>
        /// <para><paramref name="label"/> 是录的是哪个设置，显示在标题里。</para>
        /// </summary>
        public static void Record(Window owner, KeyListEditor editor, string label)
        {
            if (editor == null)
            {
                return;
            }

            var window = new ShortcutRecorderWindow(ShortcutRecorderViewModel.FromText(editor.Text), label);

            if (owner != null && !ReferenceEquals(owner, window))
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            window.ShowDialog();

            if (window.Saved)
            {
                editor.SetKeys(window.Result);
            }
        }

        /// <summary>
        /// 物理按键全在这儿收。窗口的 PreviewKeyDown 是 tunneling 里最外层的那个，比任何子控件都先拿到，
        /// 所以这里 <c>Handled = true</c> 就能拦住 Space / Enter 误按对话框自己的按钮、以及 Alt 触发访问键。
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Esc 留给「取消」，不录进去；要录 Esc 就点键盘图上的那个键帽
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            e.Handled = true;

            var virtualKey = ResolveVirtualKey(e);
            if (virtualKey == 0)
            {
                return;
            }

            HoldKey(virtualKey);

            // 单按修饰键不算一个组合；按住不放的那一串重复事件也不要一直往记录里加
            if (!KeyCodes.IsModifier(virtualKey) && !e.IsRepeat)
            {
                TryAppend(virtualKey, PhysicalModifiers().ToArray());
            }
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);

            var virtualKey = ResolveVirtualKey(e);
            if (virtualKey == 0)
            {
                return;
            }

            _heldKeys.Remove(NormalizeModifier(virtualKey));
            RefreshKeyCaps();
        }

        /// <summary>
        /// 切走再切回来的时候按住的键已经不可信了（keyup 可能压根没送到），清掉重来。
        /// </summary>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);

            _heldKeys.Clear();
            RefreshKeyCaps();
        }

        private void BuildKeyboard()
        {
            for (var i = 0; i < KeyboardLayout.Columns; i++)
            {
                KeyboardHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });
            }

            for (var i = 0; i < KeyboardLayout.Rows; i++)
            {
                KeyboardHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowPitch) });
            }

            foreach (var cap in KeyboardLayout.AllCaps)
            {
                var button = CreateCapButton(cap);

                Grid.SetColumn(button, cap.Column);
                Grid.SetRow(button, cap.Row);
                Grid.SetColumnSpan(button, cap.ColumnSpan);
                Grid.SetRowSpan(button, cap.RowSpan);
                KeyboardHost.Children.Add(button);

                var keyCode = NormalizeModifier(cap.VirtualKey);
                if (!_capsByKeyCode.TryGetValue(keyCode, out var buttons))
                {
                    _capsByKeyCode[keyCode] = buttons = new List<Button>();
                }

                buttons.Add(button);
            }
        }

        private Button CreateCapButton(KeyCap cap)
        {
            var width = cap.ColumnSpan * CellSize - CapGapX;

            var button = new Button
            {
                Content = cap.Label,
                FontSize = width >= WideCapWidth ? 12 : 10,
                Style = _capStyle,
                Tag = cap,
                ToolTip = KeyCodes.GetName(cap.VirtualKey),
                Width = width,
                Height = cap.RowSpan * RowPitch - CapGapY,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            button.Click += OnKeyCapClick;
            return button;
        }

        private void OnKeyCapClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: KeyCap cap })
            {
                return;
            }

            if (cap.Modifier)
            {
                // 点修饰键 = 按住它，再点一次 = 松开
                TogglePendingModifier(NormalizeModifier(cap.VirtualKey));
                return;
            }

            // 点普通键帽 = 按一下这个键，正在点亮的修饰键一起带上，然后清空
            TryAppend(cap.VirtualKey, _pendingModifiers.ToArray());
            _pendingModifiers.Clear();
            RefreshKeyCaps();
        }

        private void TogglePendingModifier(ushort modifier)
        {
            if (!_pendingModifiers.Remove(modifier))
            {
                _pendingModifiers.Add(modifier);
            }

            RefreshKeyCaps();
        }

        private void HoldKey(ushort virtualKey)
        {
            var keyCode = NormalizeModifier(virtualKey);
            if (!_heldKeys.Contains(keyCode))
            {
                _heldKeys.Add(keyCode);
            }

            RefreshKeyCaps();
        }

        /// <summary>把「物理按住的」和「点亮的」合成一组，点亮对应的键帽，顺便更新「当前按住」。</summary>
        private void RefreshKeyCaps()
        {
            var active = new List<ushort>(_heldKeys);
            foreach (var modifier in _pendingModifiers)
            {
                if (!active.Contains(modifier))
                {
                    active.Add(modifier);
                }
            }

            _viewModel.SetPending(active.Where(KeyCodes.IsModifier).ToList());

            foreach (var (keyCode, buttons) in _capsByKeyCode)
            {
                var on = active.Contains(keyCode);
                foreach (var button in buttons)
                {
                    button.Style = on ? _capOnStyle : _capStyle;
                }
            }
        }

        /// <summary>
        /// 录一条。名字表里没有的键不能往配置里写：<see cref="KeyCodes.GetName"/> 会给出
        /// <c>0x0C</c> 这种解析不回来的写法，一保存就会把这串按键整个清空（见 <see cref="KeyCodes.IsKnown"/>）。
        /// 真实会碰到的是 NumLock 关着的时候按小键盘 5（VK_CLEAR）。
        /// </summary>
        private void TryAppend(ushort virtualKey, IReadOnlyList<ushort> modifiers)
        {
            if (!KeyCodes.IsKnown(virtualKey))
            {
                _viewModel.SetNotice($"「{DescribeUnknownKey(virtualKey)}」没法写进配置，这一下没记下来。");
                return;
            }

            _viewModel.Append(KeyChord.Create(virtualKey, modifiers.ToArray()).Text);
        }

        private static string DescribeUnknownKey(ushort virtualKey)
        {
            return virtualKey == VirtualKeyClear ? "小键盘 5（NumLock 关着时）" : "0x" + virtualKey.ToString("X2");
        }

        /// <summary>
        /// 此刻物理按住的修饰键，顺序固定成 Ctrl / Alt / Shift / Win，和配置里的写法一致。
        /// <para>
        /// 问系统（<see cref="NativeMethods.GetKeyState"/>）而不是读 <c>Keyboard.Modifiers</c>：
        /// 对话框打开之前就按住的修饰键，WPF 那边不一定知道。传通用键码时左右两个键按住哪个都算数，
        /// 正好是配置里要的写法（混着写成 <c>Ctrl+LCtrl+V</c> 没法看）。
        /// </para>
        /// </summary>
        private static IEnumerable<ushort> PhysicalModifiers()
        {
            if (IsKeyDown(KeyCodes.Control))
            {
                yield return KeyCodes.Control;
            }

            if (IsKeyDown(KeyCodes.Alt))
            {
                yield return KeyCodes.Alt;
            }

            if (IsKeyDown(KeyCodes.Shift))
            {
                yield return KeyCodes.Shift;
            }

            if (IsKeyDown(KeyCodes.LeftWindows))
            {
                yield return KeyCodes.LeftWindows;
            }
        }

        /// <summary><c>GetKeyState</c> 的最高位是「按着」，别直接当布尔用。</summary>
        private static bool IsKeyDown(ushort virtualKey)
        {
            return (NativeMethods.GetKeyState(virtualKey) & 0x8000) != 0;
        }

        /// <summary>
        /// WPF 的按键转虚拟键码。按住 Alt 时真正的键在 <see cref="KeyEventArgs.SystemKey"/> 里，
        /// 输入法处理过的在 <see cref="KeyEventArgs.ImeProcessedKey"/> 里。
        /// </summary>
        private static ushort ResolveVirtualKey(KeyEventArgs e)
        {
            var key = e.Key switch
            {
                Key.System => e.SystemKey,
                Key.ImeProcessed => e.ImeProcessedKey,
                _ => e.Key,
            };

            return key == Key.None ? (ushort)0 : (ushort)KeyInterop.VirtualKeyFromKey(key);
        }

        /// <summary>
        /// 左右两个修饰键归一成同一个键码（<see cref="KeyCodes.LeftControl"/> → <see cref="KeyCodes.Control"/>）。
        /// 配置里混着写会变成 <c>Ctrl+LCtrl+V</c> 那种没法看的东西，键帽上也只画了一个 Ctrl。
        /// </summary>
        private static ushort NormalizeModifier(ushort virtualKey)
        {
            return virtualKey switch
            {
                KeyCodes.LeftControl or KeyCodes.RightControl => KeyCodes.Control,
                KeyCodes.LeftShift or KeyCodes.RightShift => KeyCodes.Shift,
                KeyCodes.LeftAlt or KeyCodes.RightAlt => KeyCodes.Alt,
                KeyCodes.RightWindows => KeyCodes.LeftWindows,
                _ => virtualKey,
            };
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            Saved = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Clear();
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            ApplyToChordItem(sender, _viewModel.Remove);
        }

        private void OnMoveUpClick(object sender, RoutedEventArgs e)
        {
            ApplyToChordItem(sender, _viewModel.MoveUp);
        }

        private void OnMoveDownClick(object sender, RoutedEventArgs e)
        {
            ApplyToChordItem(sender, _viewModel.MoveDown);
        }

        /// <summary>模板里的小按钮：从 DataContext 把那条记录捞出来。</summary>
        private static void ApplyToChordItem(object sender, Action<ChordItem> action)
        {
            if (sender is FrameworkElement { DataContext: ChordItem item })
            {
                action(item);
            }
        }
    }
}
