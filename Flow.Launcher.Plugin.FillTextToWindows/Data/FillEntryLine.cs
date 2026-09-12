using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Data
{
    /// <summary>
    /// 记录里的一段数据，对应 <c>FillEntryLines</c> 表的一行。
    /// <para>
    /// 三个按键字段只在「数据行配置模式」（<see cref="FillEntry.UseLineSettings"/>）开着的时候生效，
    /// 它们是这一段的独立配置，和主表的按键叠起来用：
    /// 开始前按键接在主表的后面、粘贴后按键非空时覆盖主表的「切换输入框」、
    /// 最后一段之后按键排在主表的前面。顺序见 <c>FillTextHelper.DoStartFillTextAsync</c>。
    /// </para>
    /// </summary>
    public sealed class FillEntryLine
    {
        /// <summary>这一段要粘贴的内容。</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 这一段粘贴之前发送的按键，接在主表的「开始前按键」后面。
        /// 只有第一段的会被执行（开始前按键整批只发一次）。
        /// </summary>
        public List<string> LeadingKeys { get; set; } = new();

        /// <summary>
        /// 这一段粘贴之后的按键。非空时覆盖主表的「切换输入框」，空的就还用主表的。
        /// </summary>
        public List<string> NextFieldKeys { get; set; } = new();

        /// <summary>
        /// 最后一段粘贴之后的按键，排在主表的「最后一段之后按键」前面。
        /// </summary>
        public List<string> LastFieldKeys { get; set; } = new();

        /// <summary>
        /// 复制一份，编辑窗口用它当草稿，避免改到列表里的对象。
        /// </summary>
        public FillEntryLine Clone()
        {
            return new FillEntryLine
            {
                Value = Value,
                LeadingKeys = CopyKeys(LeadingKeys),
                NextFieldKeys = CopyKeys(NextFieldKeys),
                LastFieldKeys = CopyKeys(LastFieldKeys),
            };
        }

        /// <summary>
        /// 兜底：配置里存的可能是 null（手改过 JSON），一律当成空数组，顺便复制一份。
        /// </summary>
        private static List<string> CopyKeys(List<string> keys)
        {
            return keys == null || keys.Count == 0 ? new List<string>() : new List<string>(keys);
        }
    }
}
