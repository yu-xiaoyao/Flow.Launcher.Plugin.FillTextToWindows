using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Data
{
    /// <summary>
    /// 一条保存下来的填充记录。
    /// </summary>
    public sealed class FillEntry
    {
        /// <summary>数据库主键，0 表示还没保存过。</summary>
        public long Id { get; set; } = 0;

        /// <summary>名称，Flow Launcher 里用 <c>ftw 名称</c> 搜索。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据：依次粘贴的多段文本。列表顺序就是粘贴顺序，存库时写到从表 <c>FillEntryLines.SortOrder</c>。
        /// </summary>
        public List<string> Values { get; set; } = new();

        /// <summary>是否使用下面的自定义配置；为 false 时整条记录走插件的全局配置。</summary>
        public bool UseCustomSettings { get; set; } = false;

        /// <summary>自定义配置，字段与插件全局配置完全一致，只有 <see cref="UseCustomSettings"/> 为 true 时才生效。</summary>
        public Settings CustomSettings { get; set; } = new();

        /// <summary>
        /// 算出这次填充实际要用的配置：开了自定义就用自定义的，否则用全局的。
        /// 返回的是副本，后台填充期间不会被设置面板改掉。
        /// </summary>
        public Settings ResolveSettings(Settings globalSettings)
        {
            return UseCustomSettings ? CustomSettings.Clone() : globalSettings.Clone();
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
                Values = new List<string>(Values),
                UseCustomSettings = UseCustomSettings,
                CustomSettings = CustomSettings.Clone(),
            };
        }
    }
}
