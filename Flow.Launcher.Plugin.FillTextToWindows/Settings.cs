using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flow.Launcher.Plugin.FillTextToWindows
{
    /// <summary>
    /// 插件配置。保存在 Flow Launcher 的插件设置目录里，设置面板改动后会自动写回。
    /// </summary>
    public class Settings : INotifyPropertyChanged
    {
        private List<string> _leadingKeys = new();

        private List<string> _nextFieldKeys = new()
        {
            "Tab"
        };

        private List<string> _lastFieldKeys = new();

        private int _pasteDelayMs = 40;

        private int _keyDelayMs = 40;

        private bool _restoreClipboard;

        /// <summary>
        /// 开始粘贴之前先发送的按键，留空表示不发送。
        /// 用于「进来时焦点还不在第一个输入框」的场景，例如 <c>Ctrl+Home</c>。
        /// 数组里一项就是一个按键组合，挨个发出去。
        /// </summary>
        public List<string> LeadingKeys
        {
            get => _leadingKeys;
            set => SetField(ref _leadingKeys, CopyKeys(value));
        }

        /// <summary>
        /// 每一段粘贴完成后，用来跳到下一个输入框的按键，默认 <c>Tab</c>。
        /// 表单里也可以用 <c>Down</c>，向左回退用 <c>Shift+Tab</c>。
        /// </summary>
        public List<string> NextFieldKeys
        {
            get => _nextFieldKeys;
            set => SetField(ref _nextFieldKeys, CopyKeys(value));
        }

        /// <summary>
        /// 最后一段粘贴完成之后发送的按键，留空表示不发送。
        /// 例如填完搜索框想直接提交可以填 <c>Enter</c>。
        /// </summary>
        public List<string> LastFieldKeys
        {
            get => _lastFieldKeys;
            set => SetField(ref _lastFieldKeys, CopyKeys(value));
        }

        /// <summary>
        /// 每次 Ctrl+V 之后等待目标程序处理粘贴的毫秒数。
        /// 目标程序卡顿时可以调大。
        /// </summary>
        public int PasteDelayMs
        {
            get => _pasteDelayMs;
            set => SetField(ref _pasteDelayMs, Clamp(value, 0, 10000));
        }

        /// <summary>
        /// 连续发送按键之间的间隔毫秒数。个别程序漏键时可以调大。
        /// </summary>
        public int KeyDelayMs
        {
            get => _keyDelayMs;
            set => SetField(ref _keyDelayMs, Clamp(value, 0, 10000));
        }

        /// <summary>
        /// 填充完成后是否把剪贴板还原成填充之前的内容。
        /// 文本、HTML、复制的文件等格式都能还原；图片这类存的是 GDI 句柄的格式还原不了。
        /// </summary>
        public bool RestoreClipboard
        {
            get => _restoreClipboard;
            set => SetField(ref _restoreClipboard, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 复制一份当前配置，避免后台填充过程中被设置面板改动。
        /// </summary>
        public Settings Clone()
        {
            return new Settings
            {
                LeadingKeys = LeadingKeys,
                NextFieldKeys = NextFieldKeys,
                LastFieldKeys = LastFieldKeys,
                PasteDelayMs = PasteDelayMs,
                KeyDelayMs = KeyDelayMs,
                RestoreClipboard = RestoreClipboard,
            };
        }

        /// <summary>
        /// 按键配置统一在这里兜底：配置里存的可能是 null（手改过 JSON），一律当成空数组，
        /// 顺便复制一份，免得两个 Settings 共用同一个 List。
        /// </summary>
        private static List<string> CopyKeys(List<string> keys)
        {
            return keys == null || keys.Count == 0 ? new List<string>() : new List<string>(keys);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}