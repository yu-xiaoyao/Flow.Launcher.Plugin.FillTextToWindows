using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 录制结果里的一个按键组合，例如 <c>Ctrl+V</c>。
    /// </summary>
    public sealed class ChordItem : INotifyPropertyChanged
    {
        private string _text = string.Empty;

        private int _order;

        /// <summary>组合的规范写法，就是配置里存的那个字符串。</summary>
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
        /// 序号（从 1 开始），只用于界面展示按下顺序，由 <see cref="ShortcutRecorderViewModel"/> 统一维护。
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

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
