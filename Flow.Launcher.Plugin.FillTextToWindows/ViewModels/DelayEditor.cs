using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 一个「留空就跟全局设置走」的毫秒输入框（开始前等待 / 粘贴后等待 / 按键间隔 / 最后一段之后等待）。
    /// <para>
    /// 和 <see cref="KeyListEditor"/> 一个路子：输入框里留用户敲的原文（<see cref="Text"/>），
    /// 用的时候才解析成 <see cref="Value"/>。
    /// </para>
    /// <para>
    /// 为什么不直接绑 <c>int?</c>：把框清空时 WPF 不会写出 null，转换失败就保留旧值，
    /// 看上去像清掉了、实际还生效。存原文就没这个问题。
    /// </para>
    /// </summary>
    public sealed class DelayEditor : INotifyPropertyChanged
    {
        private string _text;

        /// <summary>留空时要跟到的那个全局值，<see cref="Hint"/> 要显示出来给用户看。</summary>
        private int _globalValue;

        public DelayEditor(int? value, int globalValue)
        {
            _text = value?.ToString() ?? string.Empty;
            _globalValue = globalValue;
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
                Raise(nameof(Value));
                Raise(nameof(Hint));
            }
        }

        /// <summary>
        /// 解析出来的毫秒数。留空、或者写的不是数字，都是 null —— 也就是跟着全局设置走。
        /// </summary>
        public int? Value => int.TryParse(_text.Trim(), out var value) ? value : null;

        /// <summary>
        /// 输入框旁边的灰字。填了具体值就不提示；留空说明会跟到哪个值，
        /// 写的不是数字则提醒一句 —— 那种情况也按留空算，不说的话用户会以为自己的值生效了。
        /// </summary>
        public string Hint
        {
            get
            {
                if (Value.HasValue)
                {
                    return string.Empty;
                }

                var follow = $"跟全局走（当前 {_globalValue}）";

                return string.IsNullOrWhiteSpace(_text) ? follow : "⚠ 只能填数字，" + follow;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 换一条记录、或者换一份全局配置时同步进来。
        /// </summary>
        /// <remarks>
        /// <see cref="Text"/> 没变的话 setter 会提前返回，但全局值可能变过，
        /// 所以这里补一次 <see cref="Hint"/> 的通知。
        /// </remarks>
        public void Load(int? value, int globalValue)
        {
            _globalValue = globalValue;
            Text = value?.ToString() ?? string.Empty;
            Raise(nameof(Hint));
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
