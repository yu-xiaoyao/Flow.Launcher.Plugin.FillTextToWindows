using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Flow.Launcher.Plugin.FillTextToWindows.Data;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 编辑表单里的一个数据输入框：一段内容，外加这一段的三个按键设置。
    /// <para>
    /// 按键只在「数据行配置模式」开着的时候才用得上，界面上也是那时候才显示出来。
    /// </para>
    /// </summary>
    public sealed class ValueItem : INotifyPropertyChanged
    {
        private string _text = string.Empty;

        private int _order;

        private bool _isFirst;

        private bool _isLast;

        private List<string> _leadingKeys = new();

        private List<string> _nextFieldKeys = new();

        private List<string> _lastFieldKeys = new();

        public ValueItem()
        {
            // 三个编辑框都写回自己的字段：直接敲、点「录制」走的都是这条路
            LeadingKeys = new KeyListEditor(_leadingKeys, keys => _leadingKeys = keys);
            NextFieldKeys = new KeyListEditor(_nextFieldKeys, keys => _nextFieldKeys = keys);
            LastFieldKeys = new KeyListEditor(_lastFieldKeys, keys => _lastFieldKeys = keys);

            // 行上的延迟没有固定的全局值可跟，留空就是不额外等
            LineBeforeFillDelay = new DelayEditor(null, null);

            // 行上的填充后延迟留空或者写 0 都算没单独设，用主表那份（主表改了 EntryDraft 会来刷这里的提示）
            LineAfterFillDelay = new DelayEditor(null, null, "主表", zeroIsUnset: true);
        }

        /// <summary>这一段要粘贴的内容。</summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text == value)
                {
                    return;
                }

                _text = value;
                Raise();
            }
        }

        /// <summary>
        /// 序号（从 1 开始），只用于界面展示粘贴顺序，由 <see cref="EntryDraft"/> 统一维护。
        /// </summary>
        public int Order
        {
            get => _order;
            set
            {
                if (_order == value)
                {
                    return;
                }

                _order = value;
                Raise();
            }
        }

        /// <summary>
        /// 是不是第一段。位置由 <see cref="EntryDraft"/> 统一维护，增删数据行之后会重算。
        /// </summary>
        public bool IsFirst
        {
            get => _isFirst;
            set => SetField(ref _isFirst, value);
        }

        /// <summary>
        /// 是不是最后一段。
        /// </summary>
        public bool IsLast
        {
            get => _isLast;
            set => SetField(ref _isLast, value);
        }

        /// <summary>这一段的「填充前延迟」：这一段开始之前先等多久，留空就是不额外等。</summary>
        public DelayEditor LineBeforeFillDelay { get; }

        /// <summary>
        /// 这一段的「填充后延迟」：留空或者 0 都算没单独设，用主表的「粘贴后等待」。
        /// 提示里要显示的回落值由 <see cref="EntryDraft"/> 统一刷。
        /// </summary>
        public DelayEditor LineAfterFillDelay { get; }

        /// <summary>这一段的「开始前按键」，接在主表的后面。</summary>
        public KeyListEditor LeadingKeys { get; }

        /// <summary>这一段的「粘贴后按键」，非空时覆盖主表的「切换输入框」。</summary>
        public KeyListEditor NextFieldKeys { get; }

        /// <summary>这一段的「最后一段之后按键」，排在主表的前面。</summary>
        public KeyListEditor LastFieldKeys { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 把记录里的一段装进输入框（内容和三个按键）。
        /// </summary>
        public void LoadLine(FillEntryLine line)
        {
            Text = line?.Value ?? string.Empty;

            _leadingKeys = CopyKeys(line?.LeadingKeys);
            _nextFieldKeys = CopyKeys(line?.NextFieldKeys);
            _lastFieldKeys = CopyKeys(line?.LastFieldKeys);

            LineBeforeFillDelay.Load(line?.LineBeforeFillDelay, null);

            // 回落值传 null，等 EntryDraft 加完这一段统一刷（它才知道主表现在是哪个值）
            LineAfterFillDelay.Load(line?.LineAfterFillDelay, null);

            // 是外面换了数据，这里只同步原文，别写回去（KeyListEditor.Load 就是这么设计的）
            LeadingKeys.Load(_leadingKeys);
            NextFieldKeys.Load(_nextFieldKeys);
            LastFieldKeys.Load(_lastFieldKeys);
        }

        /// <summary>
        /// 打包成记录里的一段。
        /// </summary>
        public FillEntryLine ToLine()
        {
            return new FillEntryLine
            {
                Value = (Text ?? string.Empty).Trim(),
                LineBeforeFillDelay = LineBeforeFillDelay.Value,
                LineAfterFillDelay = LineAfterFillDelay.Value,
                LeadingKeys = CopyKeys(_leadingKeys),
                NextFieldKeys = CopyKeys(_nextFieldKeys),
                LastFieldKeys = CopyKeys(_lastFieldKeys),
            };
        }

        private static List<string> CopyKeys(List<string> keys)
        {
            return keys == null || keys.Count == 0 ? new List<string>() : new List<string>(keys);
        }

        private void SetField(ref bool field, bool value, [CallerMemberName] string propertyName = null)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Raise(propertyName);
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
