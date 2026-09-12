using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 一个毫秒输入框，可以留空表示「这一项不单独设，按默认来」。
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

        /// <summary>
        /// 留空时会跟到的那个值，<see cref="Hint"/> 要显示出来给用户看。
        /// null 表示这一项没有值可跟（比如数据行上的按键前延迟），留空就是不额外等。
        /// </summary>
        private int? _fallbackValue;

        /// <summary>写了 0 是不是也算「没单独设」。数据行上的粘贴后等待是这样的，主表那几个不是。</summary>
        private readonly bool _zeroIsUnset;

        /// <summary>提示里「跟 X 走」的那个 X：主表那几个跟的是全局设置，行上跟的是主表。</summary>
        private readonly string _fallbackLabel;

        public DelayEditor(int? value, int? fallbackValue, string fallbackLabel = "全局",
            bool zeroIsUnset = false)
        {
            _text = value?.ToString() ?? string.Empty;
            _fallbackValue = fallbackValue;
            _fallbackLabel = fallbackLabel;
            _zeroIsUnset = zeroIsUnset;
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
                Raise(nameof(Error));
            }
        }

        /// <summary>
        /// 解析出来的毫秒数。留空、或者写的不是数字，都是 null —— 也就是按默认来。
        /// </summary>
        public int? Value => int.TryParse(_text.Trim(), out var value) ? value : null;

        /// <summary>
        /// 这一项算不算「没单独设」：留空算；<see cref="_zeroIsUnset"/> 时写了 0 也算。
        /// </summary>
        public bool IsUnset
        {
            get
            {
                var value = Value;
                return value == null || (_zeroIsUnset && value == 0);
            }
        }

        /// <summary>
        /// 输入框旁边的灰字。填了具体值就不提示；算「没单独设」时说明实际会用哪个值，
        /// 写的不是数字则提醒一句 —— 那种情况也按没设算，不说的话用户会以为自己的值生效了。
        /// </summary>
        public string Hint
        {
            get
            {
                if (!IsUnset)
                {
                    return string.Empty;
                }

                // ⚠ 只在「写了东西但解析不出来」时加：留空、或者 0 这种算没设的合法写法都不算错
                var wrong = Value == null && !string.IsNullOrWhiteSpace(_text);

                var empty = _fallbackValue.HasValue
                    ? $"跟{_fallbackLabel}走（当前 {_fallbackValue.Value}）"
                    : "留空就是不等";

                return wrong ? "⚠ 只能填数字，" + empty : empty;
            }
        }

        /// <summary>
        /// 只有写法有误时才有值，正常时是空串（和 <see cref="KeyListEditor.Error"/> 一个意思）。
        /// 数据行上的延迟默认就是空的，每行都铺一句「留空就是不等」太吵，那里只显示这一条。
        /// </summary>
        public string Error
        {
            get
            {
                var hint = Hint;
                return hint.StartsWith("⚠", StringComparison.Ordinal) ? hint : string.Empty;
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
        public void Load(int? value, int? fallbackValue)
        {
            _fallbackValue = fallbackValue;
            Text = value?.ToString() ?? string.Empty;
            Raise(nameof(Hint));
            Raise(nameof(Error));
        }

        /// <summary>
        /// 只换「没单独设时会用到的那个值」，不动输入框里的原文。
        /// 主表的「粘贴后等待」一改，数据行上那几个框的提示就得跟着刷新。
        /// </summary>
        public void SetFallback(int? fallbackValue)
        {
            if (_fallbackValue == fallbackValue)
            {
                return;
            }

            _fallbackValue = fallbackValue;
            Raise(nameof(Hint));
            Raise(nameof(Error));
        }

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
