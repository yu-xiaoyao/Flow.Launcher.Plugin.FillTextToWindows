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

        public EntryDraft()
        {
            CustomSettings = new Settings();

            LeadingKeys = new KeyListEditor(CustomSettings.LeadingKeys, keys => CustomSettings.LeadingKeys = keys);
            NextFieldKeys = new KeyListEditor(CustomSettings.NextFieldKeys, keys => CustomSettings.NextFieldKeys = keys);
            LastFieldKeys = new KeyListEditor(CustomSettings.LastFieldKeys, keys => CustomSettings.LastFieldKeys = keys);

            Values.CollectionChanged += OnValuesCollectionChanged;
        }

        /// <summary>
        /// 数据：界面上就是一行一个输入框，从上到下就是粘贴顺序。
        /// 每一行还带着自己的按键设置，只有「数据行配置模式」开着时才生效。
        /// </summary>
        public ObservableCollection<ValueItem> Values { get; } = new();

        /// <summary>自定义配置，字段和插件全局配置一模一样。</summary>
        public Settings CustomSettings { get; }

        /// <summary>「开始前按键」编辑框，写回去的是 <see cref="CustomSettings"/> 上的字段。</summary>
        public KeyListEditor LeadingKeys { get; }

        /// <summary>「切换输入框」编辑框。</summary>
        public KeyListEditor NextFieldKeys { get; }

        /// <summary>「最后一段之后」编辑框。</summary>
        public KeyListEditor LastFieldKeys { get; }

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
        /// 自定义配置那 6 个字段总是有值：记录自己带自定义配置就用它的，
        /// 否则先用全局配置填上，这样用户勾上「自定义配置」时是从当前全局值开始改，而不是从空白开始。
        /// 数据行上的按键没这回事，新建时就是空的，空着的那一段自动用主表的配置。
        /// </remarks>
        public void LoadFrom(FillEntry entry, Settings globalSettings)
        {
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

            CustomSettings.CopyFrom(entry is { UseCustomSettings: true } ? entry.CustomSettings : globalSettings);

            // 配置整体换过了，三个编辑框跟着重新读一遍
            LeadingKeys.Load(CustomSettings.LeadingKeys);
            NextFieldKeys.Load(CustomSettings.NextFieldKeys);
            LastFieldKeys.Load(CustomSettings.LastFieldKeys);
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
                CustomSettings = CustomSettings.Clone(),
                UseLineSettings = UseLineSettings,
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
                    && !UseLineSettings;
            }

            if (!string.Equals(Name ?? string.Empty, entry.Name ?? string.Empty, StringComparison.Ordinal)
                || !LinesEqual(ParseLines(), entry.Values)
                || UseCustomSettings != entry.UseCustomSettings
                || UseLineSettings != entry.UseLineSettings)
            {
                return false;
            }

            return !UseCustomSettings || SettingsEqual(CustomSettings, entry.CustomSettings);
        }

        /// <summary>
        /// 内容和每一行自己的按键都要对得上，才算这一段没改过。
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
                    || !KeysEqual(left[i].LeadingKeys, right[i].LeadingKeys)
                    || !KeysEqual(left[i].NextFieldKeys, right[i].NextFieldKeys)
                    || !KeysEqual(left[i].LastFieldKeys, right[i].LastFieldKeys))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SettingsEqual(Settings left, Settings right)
        {
            if (right == null)
            {
                return false;
            }

            return KeysEqual(left.LeadingKeys, right.LeadingKeys)
                && KeysEqual(left.NextFieldKeys, right.NextFieldKeys)
                && KeysEqual(left.LastFieldKeys, right.LastFieldKeys)
                && left.PasteDelayMs == right.PasteDelayMs
                && left.KeyDelayMs == right.KeyDelayMs
                && left.RestoreClipboard == right.RestoreClipboard;
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
            Raise(nameof(SegmentSummary));
        }

        private void OnValueItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ValueItem.Text))
            {
                Raise(nameof(SegmentSummary));
            }
        }

        /// <summary>
        /// 序号跟着位置走，删掉中间一个后面的自动补上。
        /// </summary>
        private void Renumber()
        {
            for (var i = 0; i < Values.Count; i++)
            {
                Values[i].Order = i + 1;
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
