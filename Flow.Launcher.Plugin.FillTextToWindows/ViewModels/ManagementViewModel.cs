using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Flow.Launcher.Plugin.FillTextToWindows.Data;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 数据管理窗口的 ViewModel：左边列表、右边表单。
    /// </summary>
    public sealed class ManagementViewModel : INotifyPropertyChanged
    {
        private readonly FillEntryStore _store;

        private readonly Settings _globalSettings;

        private FillEntry _selectedEntry;

        /// <summary>当前表单对应的原始记录，用来判断有没有未保存的改动。</summary>
        private FillEntry _loadedSnapshot;

        private string _statusMessage = string.Empty;

        public ManagementViewModel(FillEntryStore store, Settings globalSettings)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _globalSettings = globalSettings ?? new Settings();

            Draft = new EntryDraft();

            Reload(selectId: null);
        }

        public ObservableCollection<FillEntry> Entries { get; } = new();

        public EntryDraft Draft { get; }

        public FillEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (ReferenceEquals(_selectedEntry, value))
                {
                    return;
                }

                if (value != null && !ConfirmDiscardChanges())
                {
                    // 用户选择放弃切换，让 ListBox 选回原来那条
                    Raise(nameof(SelectedEntry));
                    return;
                }

                _selectedEntry = value;
                Raise(nameof(SelectedEntry));

                Draft.LoadFrom(value, _globalSettings);
                _loadedSnapshot = value?.Clone();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                _statusMessage = value;
                Raise();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 重新从数据库读取列表。不传 <paramref name="selectId"/> 时尽量保持当前选中项。
        /// </summary>
        public void Reload(long? selectId = null)
        {
            var selectedId = selectId ?? _selectedEntry?.Id ?? 0;

            Entries.Clear();
            foreach (var entry in _store.GetAll())
            {
                Entries.Add(entry);
            }

            var match = Entries.FirstOrDefault(entry => entry.Id == selectedId);

            _selectedEntry = match;
            Raise(nameof(SelectedEntry));

            Draft.LoadFrom(match, _globalSettings);
            _loadedSnapshot = match?.Clone();

            StatusMessage = Entries.Count == 0
                ? "还没有保存过记录，填好右边的内容点「保存」就行"
                : $"共 {Entries.Count} 条记录";
        }

        /// <summary>
        /// 清空表单，录一条新的。
        /// </summary>
        public void StartNew()
        {
            if (!ConfirmDiscardChanges())
            {
                return;
            }

            _selectedEntry = null;
            Raise(nameof(SelectedEntry));

            Draft.LoadFrom(null, _globalSettings);
            _loadedSnapshot = null;

            StatusMessage = "新建记录";
        }

        /// <summary>
        /// 末尾加一个数据输入框。
        /// </summary>
        public ValueItem AddValueRow()
        {
            return Draft.AddValue();
        }

        /// <summary>
        /// 删掉一个数据输入框。
        /// </summary>
        public void RemoveValueRow(ValueItem item)
        {
            Draft.RemoveValue(item);
        }

        public void Save()
        {            var entry = Draft.ToEntry();

            if (entry.Name.Length == 0)
            {
                StatusMessage = "请先填名称，Flow Launcher 就是靠它搜到这条记录的";
                return;
            }

            if (entry.Values.Count == 0)
            {
                StatusMessage = "请至少填一段数据，空白的输入框会被跳过";
                return;
            }

            try
            {
                _store.Save(entry);
                Reload(entry.Id);
                StatusMessage = $"已保存「{entry.Name}」，共 {entry.Values.Count} 段";
            }
            catch (Exception ex)
            {
                StatusMessage = "保存失败：" + ex.Message;
                
            }
        }

        public void DeleteSelected()
        {
            if (_selectedEntry == null)
            {
                StatusMessage = "先选中左边的一条记录再删除";
                return;
            }

            var name = _selectedEntry.Name;

            var confirm = MessageBox.Show(
                $"确定删除「{name}」？",
                "FillTextToWindows 数据管理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _store.Delete(_selectedEntry.Id);
                Reload();
                StatusMessage = $"已删除「{name}」";
            }
            catch (Exception ex)
            {
                StatusMessage = "删除失败：" + ex.Message;
            }
        }

        /// <summary>
        /// 关窗口前问一句，别把没保存的编辑弄丢了。
        /// </summary>
        public bool ConfirmClose()
        {
            return ConfirmDiscardChanges();
        }

        /// <summary>
        /// 表单内容还没保存时问一句，避免切来切去把编辑丢掉。
        /// </summary>
        private bool ConfirmDiscardChanges()
        {
            if (Draft.Matches(_loadedSnapshot))
            {
                return true;
            }

            var confirm = MessageBox.Show(
                "当前编辑的内容还没保存，确定放弃吗？",
                "FillTextToWindows 数据管理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            return confirm == MessageBoxResult.Yes;
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
