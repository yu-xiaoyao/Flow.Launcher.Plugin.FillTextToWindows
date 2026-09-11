namespace Flow.Launcher.Plugin.FillTextToWindows.ViewModels
{
    /// <summary>
    /// 设置面板的 ViewModel。除了暴露 <see cref="Settings"/> 之外，
    /// 还给三个按键配置各配一个编辑框，编辑框会顺手把写法解析成可读文字，方便确认有没有写错。
    /// </summary>
    public class SettingsViewModel
    {
        public SettingsViewModel(Settings settings)
        {
            Settings = settings ?? new Settings();

            LeadingKeys = new KeyListEditor(Settings.LeadingKeys, keys => Settings.LeadingKeys = keys);
            NextFieldKeys = new KeyListEditor(Settings.NextFieldKeys, keys => Settings.NextFieldKeys = keys);
            LastFieldKeys = new KeyListEditor(Settings.LastFieldKeys, keys => Settings.LastFieldKeys = keys);
        }

        public Settings Settings { get; }

        /// <summary>「开始前先发送的按键」编辑框。</summary>
        public KeyListEditor LeadingKeys { get; }

        /// <summary>「切换输入框的按键」编辑框。</summary>
        public KeyListEditor NextFieldKeys { get; }

        /// <summary>「最后一段之后的按键」编辑框。</summary>
        public KeyListEditor LastFieldKeys { get; }
    }
}
