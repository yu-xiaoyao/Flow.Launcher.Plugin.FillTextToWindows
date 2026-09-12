using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Data
{
    /// <summary>
    /// 一条保存下来的填充记录，字段和 <c>FillEntries</c> 表一一对应。
    /// <para>
    /// 按键有三层：全局配置（<see cref="Settings"/>）、主表配置（这条记录自己的
    /// <see cref="LeadingKeys"/> 等，<see cref="UseCustomSettings"/> 总开关）、
    /// 数据行配置（<see cref="FillEntryLine"/> 上的按键，<see cref="UseLineSettings"/> 总开关）。
    /// </para>
    /// <para>
    /// 三个延迟/剪贴板字段是可空的，null 表示「跟着全局设置走」——
    /// 一条记录只覆盖它真正关心的那几项，其余随时跟着设置面板变。
    /// </para>
    /// </summary>
    public sealed class FillEntry
    {
        private List<FillEntryLine> _values = new();

        /// <summary>数据库主键，0 表示还没保存过。</summary>
        public long Id { get; set; } = 0;

        /// <summary>名称，Flow Launcher 里用 <c>ftw 名称</c> 搜索。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据：依次粘贴的多段文本，附带每一段自己的按键配置。
        /// 列表顺序就是粘贴顺序，存库时写到从表 <c>FillEntryLines.SortOrder</c>。
        /// </summary>
        public List<FillEntryLine> Values
        {
            get => _values;
            set => _values = value ?? new List<FillEntryLine>();
        }

        /// <summary>「自定义按键」总开关；为 false 时下面三个按键走插件的全局配置。</summary>
        public bool UseCustomSettings { get; set; } = false;

        /// <summary>开始粘贴之前先发送的按键，只有 <see cref="UseCustomSettings"/> 为 true 时才生效。</summary>
        public List<string> LeadingKeys { get; set; } = new();

        /// <summary>每一段粘贴完成后用来跳到下一个输入框的按键，只有 <see cref="UseCustomSettings"/> 为 true 时才生效。</summary>
        public List<string> NextFieldKeys { get; set; } = new();

        /// <summary>最后一段粘贴完成之后发送的按键，只有 <see cref="UseCustomSettings"/> 为 true 时才生效。</summary>
        public List<string> LastFieldKeys { get; set; } = new();

        /// <summary>
        /// 是否使用「数据行配置模式」：每一段用 <see cref="FillEntryLine"/> 上自己的按键。
        /// 为 true 时和主表那一层叠加，不是替换；为 false 时行上的按键一律忽略。
        /// </summary>
        public bool UseLineSettings { get; set; } = false;

        /// <summary>每次 Ctrl+V 之后等待的毫秒数；null 表示跟着全局设置走。</summary>
        public int? PasteDelayMs { get; set; } = null;

        /// <summary>连续发送按键之间的间隔毫秒数；null 表示跟着全局设置走。</summary>
        public int? KeyDelayMs { get; set; } = null;

        /// <summary>填充完成后是否还原剪贴板；null 表示跟着全局设置走。</summary>
        public bool? RestoreClipboard { get; set; } = null;

        /// <summary>
        /// 算出这次填充实际要用的配置：按键按总开关决定用哪一层，
        /// 三个延迟/剪贴板字段留空的就跟着全局设置走。返回的是副本，后台填充期间不会被界面改掉。
        /// </summary>
        public Settings ResolveSettings(Settings globalSettings)
        {
            var settings = globalSettings.Clone();

            if (UseCustomSettings)
            {
                settings.LeadingKeys = LeadingKeys;
                settings.NextFieldKeys = NextFieldKeys;
                settings.LastFieldKeys = LastFieldKeys;
            }

            // 留空的字段跟着全局走，所以这里得逐项判，不能像按键那样整层切换
            if (PasteDelayMs.HasValue)
            {
                settings.PasteDelayMs = PasteDelayMs.Value;
            }

            if (KeyDelayMs.HasValue)
            {
                settings.KeyDelayMs = KeyDelayMs.Value;
            }

            if (RestoreClipboard.HasValue)
            {
                settings.RestoreClipboard = RestoreClipboard.Value;
            }

            return settings;
        }

        /// <summary>
        /// 复制一份，编辑窗口用它当草稿，避免改到列表里的对象。
        /// </summary>
        public FillEntry Clone()
        {
            return new FillEntry
            {
                Id = Id,
                Name = Name,
                Values = Values.ConvertAll(line => line.Clone()),
                UseCustomSettings = UseCustomSettings,
                LeadingKeys = CopyKeys(LeadingKeys),
                NextFieldKeys = CopyKeys(NextFieldKeys),
                LastFieldKeys = CopyKeys(LastFieldKeys),
                UseLineSettings = UseLineSettings,
                PasteDelayMs = PasteDelayMs,
                KeyDelayMs = KeyDelayMs,
                RestoreClipboard = RestoreClipboard,
            };
        }

        /// <summary>
        /// 按键配置统一在这里兜底：存的可能是 null（手改过 JSON），一律当成空数组，顺便复制一份。
        /// </summary>
        public static List<string> CopyKeys(List<string> keys)
        {
            return keys == null || keys.Count == 0 ? new List<string>() : new List<string>(keys);
        }
    }
}
