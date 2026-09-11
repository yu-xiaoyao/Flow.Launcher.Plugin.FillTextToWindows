using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

/// <summary>
/// 一组复制/粘贴 Item
/// </summary>
public class FillTextItem
{
    /// <summary>
    /// 粘贴前延迟
    /// </summary>
    public int BeforeFillDelayMs { get; set; }

    /// <summary>
    /// 两个按键的间隔
    /// </summary>
    public int KeyDelayMs { get; set; }

    /// <summary>
    /// 粘贴后等待时间
    /// </summary>
    public int PasteDelayMs { get; set; }

    /// <summary>
    /// 填充完成后把剪贴板还原成原来的文本
    /// </summary>
    public bool RestoreClipboard { get; set; }

    /// <summary>
    ///开始前按键
    /// 粘贴第一个之前 
    /// </summary>
    public IReadOnlyList<string> LeadingKeys { get; set; }

    /// <summary>
    /// 粘贴下一个 按键
    /// </summary>
    public IReadOnlyList<string> NextFieldKeys { get; set; }

    /// <summary>
    /// 结束后按键
    /// 粘贴最后一个之后
    /// </summary>
    public IReadOnlyList<string> LastFieldKeys { get; set; }

    /// <summary>
    /// 粘贴数据行
    /// </summary>
    public IReadOnlyList<FillTextLineItem> Values { get; set; }
}