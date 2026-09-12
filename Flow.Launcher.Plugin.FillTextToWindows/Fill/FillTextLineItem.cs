using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

/// <summary>
/// 复制/粘贴 一次
/// <para>
/// 三个按键字段对应「数据行配置模式」，只有 <see cref="FillTextItem"/> 是照着
/// <c>FillEntry.UseLineSettings</c> 组装出来的时候才有值，见 <see cref="FillTextHelper.ToFillTextItem(Data.FillEntry, Settings)"/>。
/// </para>
/// </summary>
public class FillTextLineItem
{
    /// <summary>
    /// 开始前按键。主表 <see cref="FillTextItem.LeadingKeys"/> 先执行，接着才执行这个。
    /// 只认第一段的：开始前按键整批只在第一个粘贴之前发一次。
    /// </summary>
    public IReadOnlyList<string> ItemLeadingKeys { get; set; }

    /// <summary>
    /// 粘贴这一行之后发送的按键，用来跳到下一个输入框。
    /// 非空就顶掉主表的 <see cref="FillTextItem.NextFieldKeys"/>，空的话还用主表的。
    /// </summary>
    public IReadOnlyList<string> NextFieldKeys { get; set; }

    /// <summary>
    /// 最后一段粘贴之后的按键。先执行这个，再执行主表的 <see cref="FillTextItem.LastFieldKeys"/>。
    /// </summary>
    public IReadOnlyList<string> ItemLastFieldKeys { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public string TextData { get; set; }
}
