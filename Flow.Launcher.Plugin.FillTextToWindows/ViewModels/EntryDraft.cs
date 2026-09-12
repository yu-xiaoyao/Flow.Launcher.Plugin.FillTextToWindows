using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Flow.Launcher.Plugin.FillTextToWindows.Data;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 编辑窗口里的那张表单。用草稿对象而不是直接改列表里的记录，方便「没保存就切走」时提示。
    /// </summary>
    public sealed class EntryDraft : INotifyPropertyChanged
    {
        private string _name = string.Empty;

        private bool _useCustomSettings;

        private bool _useLineSettings;

        private long _entryId;

        /// <summary>「跟全局走」那几行灰字要显示当前全局值，载入的时候记一份。</summary>
        private Settings _globalSettings = new();

        private List<string> _leadingKeys = new();

        private List<string> _nextFieldKeys = new();

        private List<string> _lastFieldKeys = new();

        private bool? _restoreClipboard;

        public EntryDraft()
        {
            LeadingKeys = new KeyListEditor(_leadingKeys, keys => _leadingKeys = keys);
            NextFieldKeys = new KeyListEditor(_nextFieldKeys, keys => _nextFieldKeys = keys);
            LastFieldKeys = new KeyListEditor(_lastFieldKeys, keys => _lastFieldKeys = keys);

            // 全局值载入的时候才填进去，先摆个 0
            BeforeFillDelay = new DelayEditor(null, 0);
            PasteDelay = new DelayEditor(null, 0);
            KeyDelay = new DelayEditor(null, 0);

            // 行上的「填充后延迟」留空就跟主表走，主表这个框一改就得把提示刷一遍
            PasteDelay.PropertyChanged += OnPasteDelayChanged;

            Values.CollectionChanged += OnValuesCollectionChanged;
        }

        /// <summary>
        /// 数据：界面上就是一行一个输入框，从上到下就是粘贴顺序。
        /// 每一行还带着自己的按键设置，只有「数据行配置模式」开着时才生效。
        /// </summary>
        public ObservableCollection<ValueItem> Values { get; } = new();

        /// <summary>「开始前按键」编辑框，写回本条记录自己的按键。</summary>
        public KeyListEditor LeadingKeys { get; }

        /// <summary>「切换输入框」编辑框。</summary>
        public KeyListEditor NextFieldKeys { get; }

        /// <summary>「最后一段之后」编辑框。</summary>
        public KeyListEditor LastFieldKeys { get; }

        /// <summary>「开始前等待」：留空就跟着全局设置走。</summary>
        public DelayEditor BeforeFillDelay { get; }

        /// <summary>「粘贴后等待」：留空就跟着全局设置走。</summary>
        public DelayEditor PasteDelay { get; }

        /// <summary>「按键间隔」：留空就跟着全局设置走。</summary>
        public DelayEditor KeyDelay { get; }

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public bool UseCustomSettings
        {
            get => _useCustomSettings;
            set => SetField(ref _useCustomSettings, value);
        }

        /// <summary>
        /// 「数据行配置模式」总开关。开着的时候数据行上的按键才生效，和主表的按键叠加。
        /// </summary>
        public bool UseLineSettings
        {
            get => _useLineSettings;
            set => SetField(ref _useLineSettings, value);
        }

        /// <summary>
        /// 「还原剪贴板」。三态：勾上还原、空着不还原、半选跟着全局设置走。
        /// </summary>
        public bool? RestoreClipboard
        {
            get => _restoreClipboard;
            set
            {
                if (SetField(ref _restoreClipboard, value))
                {
                    Raise(nameof(RestoreClipboardHint));
                }
            }
        }

        public string RestoreClipboardHint => RestoreClipboard.HasValue
            ? string.Empty
            : "跟全局走（当前" + (_globalSettings.RestoreClipboard ? "还原" : "不还原") + "）";

        public string SegmentSummary
        {
            get
            {
                var count = ParseValues().Count;

                if (Values.Count == 0)
                {
                    return "还没有数据，点下面的「+ 添加数据」";
                }

                if (count == 0)
                {
                    return "现在全是空白，粘贴时会跳过";
                }

                return count == Values.Count
                    ? $"共 {count} 段，从上到下依次粘贴"
                    : $"共 {count} 段（空白项已忽略），从上到下依次粘贴";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 把一条记录装进表单。<paramref name="entry"/> 为 null 表示新建。
        /// </summary>
        /// <remarks>
        /// 三个按键总是有值：记录自己带就用它的，没勾「自定义按键」或者新建时先用全局按键填上，
        /// 这样用户勾上之后是从当前全局值开始改，而不是从空白开始。
        /// 三个延迟和剪贴板开关没这回事：没存过就留空，表示跟着全局设置走。
        /// 数据行上的按键也留空，空着的那一段自动用主表的配置。
        /// </remarks>
        public void LoadFrom(FillEntry entry, Settings globalSettings)
        {
            _globalSettings = globalSettings ?? new Settings();

            _entryId = entry?.Id ?? 0;
            Name = entry?.Name ?? string.Empty;
            UseCustomSettings = entry?.UseCustomSettings ?? false;
            UseLineSettings = entry?.UseLineSettings ?? false;

            Values.Clear();

            if (entry == null)
            {
                // 新建时先摆一个空框，点开就能直接敲
                AddValue();
            }
            else
            {
                foreach (var line in entry.Values)
                {
                    AddLine(line);
                }
            }

            // 没勾「自定义按键」的记录，编辑框里摆当前的全局按键：勾选框是灰的，看得见但改不了
            var useCustom = entry is { UseCustomSettings: true };

            _leadingKeys = FillEntry.CopyKeys(useCustom ? entry.LeadingKeys : globalSettings.LeadingKeys);
            _nextFieldKeys = FillEntry.CopyKeys(useCustom ? entry.NextFieldKeys : globalSettings.NextFieldKeys);
            _lastFieldKeys = FillEntry.CopyKeys(useCustom ? entry.LastFieldKeys : globalSettings.LastFieldKeys);

            LeadingKeys.Load(_leadingKeys);
            NextFieldKeys.Load(_nextFieldKeys);
            LastFieldKeys.Load(_lastFieldKeys);

            // 这几个留空就是跟着全局走，所以没存过的记录摆空框，旁边灰字显示会跟到哪个值
            BeforeFillDelay.Load(entry?.BeforeFillDelayMs, _globalSettings.BeforeFillDelayMs);
            PasteDelay.Load(entry?.PasteDelayMs, _globalSettings.PasteDelayMs);
            KeyDelay.Load(entry?.KeyDelayMs, _globalSettings.KeyDelayMs);

            RestoreClipboard = entry?.RestoreClipboard;
            Raise(nameof(RestoreClipboardHint));
        }

        /// <summary>
        /// 末尾加一个空输入框。
        /// </summary>
        public ValueItem AddValue(string text = "")
        {
            var item = new ValueItem { Text = text ?? string.Empty };
            Values.Add(item);
            return item;
        }

        /// <summary>
        /// 末尾加一个输入框，内容和三个按键都按记录里的一段填好。
        /// </summary>
        private ValueItem AddLine(FillEntryLine line)
        {
            var item = new ValueItem();
            item.LoadLine(line);
            Values.Add(item);
            return item;
        }

        public void RemoveValue(ValueItem item)
        {
            if (item != null)
            {
                Values.Remove(item);
            }
        }

        /// <summary>
        /// 把表单内容变成一条记录，<c>Id</c> 为 0 表示要新增。
        /// </summary>
        public FillEntry ToEntry()
        {
            return new FillEntry
            {
                Id = _entryId,
                Name = (Name ?? string.Empty).Trim(),
                Values = ParseLines(),
                UseCustomSettings = UseCustomSettings,
                LeadingKeys = FillEntry.CopyKeys(_leadingKeys),
                NextFieldKeys = FillEntry.CopyKeys(_nextFieldKeys),
                LastFieldKeys = FillEntry.CopyKeys(_lastFieldKeys),
                UseLineSettings = UseLineSettings,
                BeforeFillDelayMs = BeforeFillDelay.Value,
                PasteDelayMs = PasteDelay.Value,
                KeyDelayMs = KeyDelay.Value,
                RestoreClipboard = RestoreClipboard,
            };
        }

        /// <summary>
        /// 按界面顺序取出真正要保存的数据行（内容和每一行自己的按键），空白项直接跳过。
        /// </summary>
        public List<FillEntryLine> ParseLines()
        {
            var lines = new List<FillEntryLine>();

            foreach (var item in Values)
            {
                var line = item.ToLine();
                if (line.Value.Length > 0)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>
        /// 只要内容，界面上用来数这一条有几段。
        /// </summary>
        public List<string> ParseValues()
        {
            return ParseLines().Select(line => line.Value).ToList();
        }

        /// <summary>
        /// 表单内容是否和指定记录完全一致，用来判断有没有未保存的修改。
        /// </summary>
        public bool Matches(FillEntry entry)
        {
            if (entry == null)
            {
                return string.IsNullOrWhiteSpace(Name)
                    && Values.All(item => string.IsNullOrWhiteSpace(item.Text))
                    && !UseCustomSettings
                    && !UseLineSettings
                    && BeforeFillDelay.Value == null
                    && PasteDelay.Value == null
                    && KeyDelay.Value == null
                    && RestoreClipboard == null;
            }

            if (!string.Equals(Name ?? string.Empty, entry.Name ?? string.Empty, StringComparison.Ordinal)
                || !LinesEqual(ParseLines(), entry.Values)
                || UseCustomSettings != entry.UseCustomSettings
                || UseLineSettings != entry.UseLineSettings
                || BeforeFillDelay.Value != entry.BeforeFillDelayMs
                || PasteDelay.Value != entry.PasteDelayMs
                || KeyDelay.Value != entry.KeyDelayMs
                || RestoreClipboard != entry.RestoreClipboard)
            {
                return false;
            }

            // 没勾「自定义按键」时三个框里摆的是全局按键的副本，改了也不生效，不用算进「改过没」
            return !UseCustomSettings
                   || (KeysEqual(_leadingKeys, entry.LeadingKeys)
                       && KeysEqual(_nextFieldKeys, entry.NextFieldKeys)
                       && KeysEqual(_lastFieldKeys, entry.LastFieldKeys));
        }

        /// <summary>
        /// 内容、每一段的按键前 / 按键后延迟和三个按键都要对得上，才算这一段没改过。
        /// </summary>
        private static bool LinesEqual(IReadOnlyList<FillEntryLine> left, IReadOnlyList<FillEntryLine> right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i].Value ?? string.Empty, right[i].Value ?? string.Empty, StringComparison.Ordinal)
                    || left[i].LineBeforeFillDelay != right[i].LineBeforeFillDelay
                    || left[i].LineAfterFillDelay != right[i].LineAfterFillDelay
                    || !KeysEqual(left[i].LeadingKeys, right[i].LeadingKeys)
                    || !KeysEqual(left[i].NextFieldKeys, right[i].NextFieldKeys)
                    || !KeysEqual(left[i].LastFieldKeys, right[i].LastFieldKeys))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool KeysEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            return (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        private void OnValuesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ValueItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnValueItemChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (ValueItem item in e.NewItems)
                {
                    item.PropertyChanged += OnValueItemChanged;
                }
            }

            Renumber();
            ApplyLineAfterFillDelayFallback();
            Raise(nameof(SegmentSummary));
        }

        /// <summary>
        /// 主表的「粘贴后等待」改了，每一行那个框的提示要跟着变。
        /// </summary>
        private void OnPasteDelayChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DelayEditor.Value))
            {
                ApplyLineAfterFillDelayFallback();
            }
        }

        /// <summary>
        /// 把主表当前的「粘贴后等待」告诉每一行：行上留空或者写 0 的时候用的就是它。
        /// 主表这个值本身也可能是留空的（跟着全局设置走），那就用设置面板里的那个数。
        /// </summary>
        private void ApplyLineAfterFillDelayFallback()
        {
            var fallback = PasteDelay.Value ?? _globalSettings.PasteDelayMs;

            foreach (var item in Values)
            {
                item.LineAfterFillDelay.SetFallback(fallback);
            }
        }

        private void OnValueItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ValueItem.Text))
            {
                Raise(nameof(SegmentSummary));
            }
        }

        /// <summary>
        /// 序号跟着位置走，删掉中间一个后面的自动补上；第一段 / 最后一段也跟着重算，
        /// 数据行上该显示哪几个按键（开始前 / 粘贴后 / 最后之后）就是按这个来的。
        /// </summary>
        private void Renumber()
        {
            for (var i = 0; i < Values.Count; i++)
            {
                Values[i].Order = i + 1;
                Values[i].IsFirst = i == 0;
                Values[i].IsLast = i == Values.Count - 1;
            }
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            Raise(propertyName);
            return true;
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
