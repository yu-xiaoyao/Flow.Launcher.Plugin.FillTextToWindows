using System.Collections.Generic;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

/// <summary>
/// 复制/粘贴 一次
/// </summary>
public class FillTextLineItem
{
    /// <summary>
    ///开始前按键, 如果 FillTextItem 中的 LeadingKeys 有值会先执行, 然后再执行  ItemLeadingKeys
    /// </summary>
    public IReadOnlyList<string> ItemLeadingKeys { get; set; }

    /// <summary>
    /// 粘贴下一个按键, 如果 FillTextItem 中的 NextFieldKeys 有值会被忽略, 直接执行当前的 NextFieldKeys
    /// 特别注意
    /// </summary>
    public IReadOnlyList<string> NextFieldKeys { get; set; }

    /// <summary>
    /// 结束后按键 如果 FillTextItem 中的 LastFieldKeys 有值会先执行, 然后再执行  ItemLastFieldKeys
    /// </summary>
    public IReadOnlyList<string> ItemLastFieldKeys { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public string TextData { get; set; }
}