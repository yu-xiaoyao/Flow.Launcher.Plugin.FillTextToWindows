using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 一个按键配置（开始前 / 切换输入框 / 最后一段之后）的编辑框。
    /// <para>
    /// 按键在配置里存成字符串数组，界面上却是一个输入框，所以这里两头都要管：
    /// 输入框里保留用户自己敲的原文（<see cref="Text"/>，不重排，光标不会跳），
    /// 每敲一下顺手解析一遍，解析成功就写回配置，失败就写空并让 <see cref="Preview"/> 显示 ⚠ 提示。
    /// </para>
    /// </summary>
    public sealed class KeyListEditor : INotifyPropertyChanged
    {
        private readonly Action<List<string>> _store;

        private string _text;

        public KeyListEditor(IReadOnlyList<string> keys, Action<List<string>> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _text = KeyParser.Format(keys);
        }

        /// <summary>输入框里的原文。</summary>
        public string Text
        {
            get => _text;
            set
            {
                var text = value ?? string.Empty;
                if (string.Equals(_text, text, StringComparison.Ordinal))
                {
                    return;
                }

                _text = text;
                Raise(nameof(Text));
                Raise(nameof(Preview));
                Push();
            }
        }

        /// <summary>解析结果的说明，写错的时候直接显示错在哪。</summary>
        public string Preview => KeyParser.DescribeText(_text);

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 用一串按键整个替换输入框里的内容，和用户自己敲 <see cref="Text"/> 一样会写回配置。
        /// 录制对话框点保存走的就是这里。
        /// </summary>
        public void SetKeys(IReadOnlyList<string> keys)
        {
            var text = KeyParser.Format(keys);
            if (string.Equals(_text, text, StringComparison.Ordinal))
            {
                return;
            }

            _text = text;
            Raise(nameof(Text));
            Raise(nameof(Preview));
            Push();
        }

        /// <summary>
        /// 配置被换掉了（切到别的记录、或者重新载入），把原文同步过来。
        /// <para>注意这里故意不 <see cref="Push"/>：是外面改了配置，再写回去就成了自己覆盖自己。</para>
        /// </summary>
        public void Load(IReadOnlyList<string> keys)
        {
            var text = KeyParser.Format(keys);
            if (string.Equals(_text, text, StringComparison.Ordinal))
            {
                return;
            }

            _text = text;
            Raise(nameof(Text));
            Raise(nameof(Preview));
        }

        private void Push()
        {
            _store(KeyParser.TryToConfig(_text, out var config, out _) ? config : new List<string>());
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
