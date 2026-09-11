using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 按键录制对话框里录下来的东西。
    /// <para>
    /// 每按一次组合键就往 <see cref="Chords"/> 里加一条，顺序就是按下的顺序，
    /// 也就是之后填充时依次发送的顺序。保存时 <see cref="Result"/> 会被写回输入框。
    /// </para>
    /// </summary>
    public sealed class ShortcutRecorderViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<ChordItem> _chords = new();

        private string _pendingText = string.Empty;

        private string _notice = string.Empty;

        /// <summary>
        /// 从输入框里的原文开始录。能解析就按解析结果拆成一条条；解析不了（手改过配置、或者上次就写错了）
        /// 就把原文整体当成一条带过来，免得一打开对话框就把用户写的东西吃掉。
        /// </summary>
        public static ShortcutRecorderViewModel FromText(string text)
        {
            var viewModel = new ShortcutRecorderViewModel();

            var chords = KeyParser.ParseOrDefault(text);
            if (chords.Count > 0)
            {
                foreach (var chord in chords)
                {
                    viewModel.Append(chord.Text);
                }

                return viewModel;
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                viewModel.Append(text.Trim());
            }

            return viewModel;
        }

        /// <summary>录到的按键，一条是一个组合。</summary>
        public ObservableCollection<ChordItem> Chords => _chords;

        /// <summary>录到东西了没有，用来显示空状态、禁用「清空」。</summary>
        public bool HasChords => _chords.Count > 0;

        /// <summary>最终会写进输入框的内容，写错的时候带 ⚠ 说明。</summary>
        public string Preview => KeyParser.DescribeText(KeyParser.Format(Result));

        /// <summary>当前按住（或者点亮）的修饰键，例如 <c>Ctrl + Shift</c>，没有就是空。</summary>
        public string PendingText
        {
            get => _pendingText;
            private set
            {
                if (string.Equals(_pendingText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _pendingText = value;
                Raise();
            }
        }

        /// <summary>保存时写回输入框的内容。</summary>
        public List<string> Result => _chords.Select(item => item.Text).ToList();

        /// <summary>
        /// 录一条。故意不去重：连按两下同一个键（<c>Down, Down</c>）是正常写法。
        /// </summary>
        public void Append(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _chords.Add(new ChordItem { Text = text.Trim() });
            Notice = string.Empty;
            Refresh();
        }

        /// <summary>删掉一条。</summary>
        public void Remove(ChordItem item)
        {
            // 拿不准调用方传进来的是不是本列表里的那一个，先找位置再删
            var index = _chords.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            _chords.RemoveAt(index);
            Refresh();
        }

        /// <summary>把一条往前挪一位。</summary>
        public void MoveUp(ChordItem item)
        {
            var index = _chords.IndexOf(item);
            if (index > 0)
            {
                _chords.Move(index, index - 1);
                Refresh();
            }
        }

        /// <summary>把一条往后挪一位。</summary>
        public void MoveDown(ChordItem item)
        {
            var index = _chords.IndexOf(item);
            if (index >= 0 && index < _chords.Count - 1)
            {
                _chords.Move(index, index + 1);
                Refresh();
            }
        }

        /// <summary>清空重录。</summary>
        public void Clear()
        {
            if (_chords.Count == 0)
            {
                return;
            }

            _chords.Clear();
            Refresh();
        }

        /// <summary>更新「当前按住」那一行。传进来的顺序就是要显示的顺序。</summary>
        public void SetPending(IReadOnlyList<ushort> modifiers)
        {
            PendingText = modifiers == null || modifiers.Count == 0
                ? string.Empty
                : string.Join(" + ", modifiers.Select(code => KeyCodes.GetName(code)));
        }

        /// <summary>
        /// 录的过程中给用户的一句话（比如按了个写不进配置的键），没有就是空。
        /// 录成功了会自己清掉。
        /// </summary>
        public string Notice
        {
            get => _notice;
            private set
            {
                if (string.Equals(_notice, value, StringComparison.Ordinal))
                {
                    return;
                }

                _notice = value;
                Raise();
            }
        }

        /// <summary>显示一句提示，见 <see cref="Notice"/>。</summary>
        public void SetNotice(string text)
        {
            Notice = text ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>条目增删挪动之后序号、预览、空状态都得跟着变。</summary>
        private void Refresh()
        {
            for (var i = 0; i < _chords.Count; i++)
            {
                _chords[i].Order = i + 1;
            }

            Raise(nameof(HasChords));
            Raise(nameof(Preview));
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
