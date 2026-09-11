using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 编辑表单里的一个数据输入框。
    /// </summary>
    public sealed class ValueItem : INotifyPropertyChanged
    {
        private string _text = string.Empty;

        private int _order;

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
