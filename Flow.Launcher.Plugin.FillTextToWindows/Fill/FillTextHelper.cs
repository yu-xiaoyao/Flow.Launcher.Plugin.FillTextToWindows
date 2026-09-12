using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flow.Launcher.Plugin.FillTextToWindows.Data;
using Flow.Launcher.Plugin.FillTextToWindows.Interop;
using Flow.Launcher.Plugin.FillTextToWindows.Keys;
using Flow.Launcher.Plugin.FillTextToWindows.Util;
using JetBrains.Annotations;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

public class FillTextHelper
{
    [CanBeNull] private static FillTextTaskMetadata _metadata = null;

    public static void ResetFill()
    {
        _metadata = null;
    }

    public static void SetFillItem(FillTextItem item)
    {
        _metadata = new FillTextTaskMetadata(item);
    }

    public static async Task StartFill()
    {
        var metadata = _metadata;
        InnerLogger.Logger.Debug($"开始填充. metadata = {metadata}");
        if (metadata == null) return;
        if (!metadata.TryStart())
        {
            InnerLogger.Logger.Debug("这次填充已经被别的后台任务接管, 跳过。");
            return;
        }

        // Timeout....
        if (DateTime.Now - metadata.StartTime <= TimeSpan.FromSeconds(10))
        {
            await StartFillTextAsync(metadata);
        }
    }

    public static async Task StartFillTextAsync(FillTextTaskMetadata metadata)
    {
        ClipboardHelper.ClipboardSnapshot clipboardOrigin = null;
        if (metadata.Item.RestoreClipboard)
        {
            // 这里跑在 Task.Run 的线程池线程（MTA）上，只能用裸 Win32 剪贴板，
            // WPF 的 System.Windows.Clipboard 要求 STA，会直接抛 ThreadStateException。
            clipboardOrigin = ClipboardHelper.Capture();
        }

        InnerLogger.Logger.Trace("开始填充任务");

        await DoStartFillTextAsync(metadata);

        // restore Clipboard Object
        if (clipboardOrigin == null)
        {
            return;
        }

        if (!ReferenceEquals(_metadata, metadata))
        {
            // 已经换了一个新的填充任务（或者被 Reset 了），这时候还原会把新任务刚写进去的内容覆盖掉
            InnerLogger.Logger.Trace("填充已被新任务接管. 跳过恢复剪贴板");
            return;
        }

        InnerLogger.Logger.Trace("填充结束. 恢复原始剪贴板数据");

        if (!ClipboardHelper.Restore(clipboardOrigin))
        {
            InnerLogger.Logger.Warn("恢复剪贴板失败（可能被其它程序占用）。");
        }
    }

    public static async Task DoStartFillTextAsync(FillTextTaskMetadata metadata)
    {
        var item = metadata.Item;

        InnerLogger.Logger.Debug($"{DescribeFlow(item)}");

        var keyDelayMs = item.KeyDelayMs;
        var values = item.Values;
        var size = values.Count;
        if (size <= 0) return;

        // 开始之前等待
        var success = await WaitMills(metadata, item.BeforeFillDelayMs);
        if (!success)
        {
            InnerLogger.Logger.Trace("开始之前等待. 失败");
            return;
        }

        // 发送开始之前的按键
        success = await SendKeys(metadata, item.LeadingKeys, keyDelayMs);
        if (!success)
        {
            InnerLogger.Logger.Trace("发送开始之前的按键. 失败");
            return;
        }

        // 开始按行复制粘贴数据。
        // 行上的按键和延迟（数据行配置模式）和主表是叠加关系：
        //   填充前延迟：每一段等自己这一份，在主表的开始前等待和按键之后；
        //   填充后延迟：填了（大于 0）就顶掉主表的「粘贴后等待」，留空或者 0 继续用主表的；
        //   开始前按键：主表发完接着发这一行的，整批只在第一个粘贴之前发一次；
        //   粘贴后按键：非空就顶掉主表的，空的话继续用主表的；
        //   最后一段之后按键：发在这个循环之后、主表的最后一段之后按键之前。
        for (var i = 0; i < values.Count; i++)
        {
            var lineItem = values[i];

            // line before key delay
            success = await WaitMills(metadata, lineItem.LineBeforeFillDelay);
            if (!success) return;

            if (i == 0)
            {
                // 发送前执行按键
                success = await SendKeys(metadata, lineItem.ItemLeadingKeys, keyDelayMs);
                if (!success) return;
            }

            // 开始复制/粘贴.
            success = await FillText(metadata, lineItem.TextData, item.PasteDelayMs);
            if (!success) return;

            // line after key delay
            success = await WaitMills(metadata, lineItem.LineAfterFillDelay);
            if (!success) return;

            var isLast = i == values.Count - 1;
            if (isLast)
            {
                // last
                success = await SendKeys(metadata, lineItem.ItemLastFieldKeys, keyDelayMs);
            }
            else
            {
                var itemNextFieldKeys = lineItem.NextFieldKeys;
                if (itemNextFieldKeys == null || itemNextFieldKeys.Count == 0)
                {
                    itemNextFieldKeys = item.NextFieldKeys;
                }

                success = await SendKeys(metadata, itemNextFieldKeys, keyDelayMs);
            }

            if (!success) return;
        }
        // end loop

        await SendKeys(metadata, item.LastFieldKeys, keyDelayMs);
    }

    private static async Task<bool> FillText(FillTextTaskMetadata metadata, string textData, int pasteDelayMs)
    {
        InnerLogger.Logger.Trace($"开始填充Text. textData: {textData}");

        if (!ClipboardHelper.SetText(textData))
        {
            InnerLogger.Logger.Warn("写入剪贴板失败（可能被其它程序占用），已停止填充。");
            return false;
        }

        await WaitMills(metadata, pasteDelayMs);

        if (!KeyboardSimulator.Send(KeyboardSimulator.Paste))
        {
            InnerLogger.Logger.Warn("模拟 Ctrl+V 失败，已停止填充。");
            return false;
        }

        return true;
    }

    public static async Task<bool> SendKeys(FillTextTaskMetadata metadata, IReadOnlyList<string> keys, int keyDelayMs)
    {
        if (keys == null || keys.Count <= 0)
            return true;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var keyChord = KeyParser.ParseOrDefault(key);
            if (keyChord.Count > 0)
            {
                KeyboardSimulator.Send(keyChord);
            }
            else
            {
                // 解析 Key 失败
                return false;
            }
        }

        return await WaitMills(metadata, keyDelayMs);
    }

    private static async Task<bool> WaitMills(FillTextTaskMetadata metadata, int waitMs)
    {
        if (waitMs > 0)
        {
            await Task.Delay(waitMs).ConfigureAwait(false);
        }

        var md = _metadata;
        if (md == null)
        {
            // 终止当前任务
            return false;
        }

        // ID 相同, 不终止; ID 发生变更则终止当前任务
        return string.Equals(metadata.Id, md.Id, StringComparison.Ordinal);
    }


    public static FillTextItem ToFillTextItem(IReadOnlyList<string> lineTextList, Settings settings)
    {
        var values = lineTextList.Select(lineText => new FillTextLineItem
        {
            LineBeforeFillDelay = 0,
            LineAfterFillDelay = settings.PasteDelayMs,
            ItemLeadingKeys = new List<string>(),
            NextFieldKeys = new List<string>(),
            ItemLastFieldKeys = new List<string>(),
            TextData = lineText
        }).ToList();

        return BuildFillTextItem(settings, values);
    }

    /// <summary>
    /// 把保存过的记录组装成一次填充任务。
    /// <para>
    /// 开了「数据行配置模式」（<see cref="FillEntry.UseLineSettings"/>）时，每一段带上自己的延迟和按键；
    /// 没开就都是空的，执行时自动回落成主表那一套。
    /// </para>
    /// </summary>
    public static FillTextItem ToFillTextItem(FillEntry entry, Settings settings)
    {
        var useLineSettings = entry?.UseLineSettings ?? false;
        var lines = entry?.Values ?? new List<FillEntryLine>();

        var values = lines.Select(line => new FillTextLineItem
        {
            TextData = line.Value,
            LineBeforeFillDelay = LineBeforeFillDelay(useLineSettings, line.LineBeforeFillDelay),
            LineAfterFillDelay = LineAfterFillDelay(useLineSettings, line.LineAfterFillDelay),
            ItemLeadingKeys = LineKeys(useLineSettings, line.LeadingKeys),
            NextFieldKeys = LineKeys(useLineSettings, line.NextFieldKeys),
            ItemLastFieldKeys = LineKeys(useLineSettings, line.LastFieldKeys),
        }).ToList();

        return BuildFillTextItem(settings, values);
    }

    /// <summary>
    /// 行上的按键配置：没开数据行配置模式就是空数组，执行时一律走主表。
    /// </summary>
    private static IReadOnlyList<string> LineKeys(bool useLineSettings, List<string> keys)
    {
        return useLineSettings && keys != null ? keys : new List<string>();
    }

    /// <summary>
    /// 行上的填充前延迟：没开数据行配置模式、或者这一段没填，都是 0（不额外等）。
    /// </summary>
    private static int LineBeforeFillDelay(bool useLineSettings, int? delayMs)
    {
        return useLineSettings && delayMs.HasValue ? delayMs.Value : 0;
    }

    /// <summary>
    /// 行上的填充后延迟：留空或者 0 都算「没单独设」，用主表 / 全局那份；
    /// 没开数据行配置模式时同样一律走主表。
    /// </summary>
    private static int LineAfterFillDelay(bool useLineSettings, int? delayMs)
    {
        return useLineSettings && delayMs.HasValue ? delayMs.Value : 0;
    }

    private static FillTextItem BuildFillTextItem(Settings settings, IReadOnlyList<FillTextLineItem> values)
    {
        return new FillTextItem
        {
            BeforeFillDelayMs = settings.BeforeFillDelayMs,
            KeyDelayMs = settings.KeyDelayMs,
            PasteDelayMs = settings.PasteDelayMs,
            RestoreClipboard = settings.RestoreClipboard,
            LeadingKeys = settings.LeadingKeys,
            NextFieldKeys = settings.NextFieldKeys,
            LastFieldKeys = settings.LastFieldKeys,
            Values = values,
        };
    }

    /// <summary>
    /// 把一个填充任务描述成「操作流程」文本：一步一行，顺序和
    /// <see cref="DoStartFillTextAsync"/> 一模一样，没有按键或内容的那几步自动省掉。
    /// <para>
    /// 用来预览这条记录到底会干什么，出问题时也可以直接打进日志。例子：
    /// </para>
    /// <code>
    /// 延迟：每段粘贴 40 毫秒，每次按键 40 毫秒
    /// 1. 等待 300 毫秒
    /// 2. 按 Ctrl+Home
    /// 3. 等待 100 毫秒
    /// 4. 按 Ctrl+A
    /// 5. 粘贴「张三」
    /// 6. 按 Tab
    /// 7. 粘贴「13800138000」
    /// 8. 按 Down
    /// 9. 按 Down
    /// 10. 粘贴「北京」
    /// 11. 按 Ctrl+S
    /// 12. 按 Enter
    /// </code>
    /// </summary>
    public static string DescribeFlow(FillTextItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        var steps = new List<string>();

        if (item.BeforeFillDelayMs > 0)
        {
            steps.Add($"等待 {item.BeforeFillDelayMs} 毫秒");
        }

        AddSendSteps(steps, item.LeadingKeys);

        var values = item.Values ?? new List<FillTextLineItem>();

        for (var i = 0; i < values.Count; i++)
        {
            var lineItem = values[i];
            var level1Space = $"->行: {i + 1}.";
            // 这一段自己的填充前延迟
            if (lineItem.LineBeforeFillDelay > 0)
            {
                steps.Add($"{level1Space} 等待 {lineItem.LineBeforeFillDelay} 毫秒");
            }

            // 开始前按键只认第一段的，这里和执行那边保持一致
            if (i == 0)
            {
                AddSendSteps(steps, lineItem.ItemLeadingKeys, level1Space);
            }

            // 这一段自己的填充后延迟，和主表不一样时才写出来（一样的话开头那行已经说过）
            var paste = $"{level1Space} 粘贴「{Shorten(lineItem.TextData)}」";
            if (lineItem.LineAfterFillDelay != item.PasteDelayMs)
            {
                paste += $"{level1Space} (先等 {lineItem.LineAfterFillDelay} 毫秒)";
            }

            steps.Add(paste);

            if (i == values.Count - 1)
            {
                AddSendSteps(steps, lineItem.ItemLastFieldKeys, level1Space);
            }
            else
            {
                var nextFieldKeys = lineItem.NextFieldKeys;
                if (nextFieldKeys == null || nextFieldKeys.Count == 0)
                {
                    nextFieldKeys = item.NextFieldKeys;
                }

                AddSendSteps(steps, nextFieldKeys, level1Space);
            }
        }

        AddSendSteps(steps, item.LastFieldKeys);

        var flow = string.Join(
            Environment.NewLine,
            steps.Select((step, index) => $"{index + 1}. {step}"));

        var delays = DescribeDelays(item);

        return delays.Length == 0 ? flow : delays + Environment.NewLine + flow;
    }

    /// <summary>
    /// 按键一个组合一步，和 <see cref="SendKeys"/> 里挨个发出去是对应的。
    /// 写法有问题的会显示成 <c>⚠ ...</c>，正好在流程里就能看出是哪一步。
    /// </summary>
    private static void AddSendSteps(List<string> steps, IReadOnlyList<string> keys, string levelSpace = "")
    {
        if (keys == null)
        {
            return;
        }

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            steps.Add(levelSpace + "按 " + KeyParser.Describe(new[] { key }));
        }
    }

    /// <summary>
    /// 两个反复出现的延迟放在开头说一次，不然每一步后面都缀一句没法看。
    /// 开始前等待本身就是一个步骤；行上的粘贴后等待和主表不一样时写在那个粘贴步骤上，所以都不在这里。
    /// </summary>
    private static string DescribeDelays(FillTextItem item)
    {
        var parts = new List<string>();

        if (item.PasteDelayMs > 0)
        {
            parts.Add($"每段粘贴 {item.PasteDelayMs} 毫秒");
        }

        if (item.KeyDelayMs > 0)
        {
            parts.Add($"每次按键 {item.KeyDelayMs} 毫秒");
        }

        return parts.Count == 0 ? string.Empty : "延迟：" + string.Join("，", parts);
    }

    /// <summary>内容太长就截一下，流程文本一行别被撑爆；换行也压成空格。</summary>
    private static string Shorten(string text)
    {
        const int maxLength = 30;

        var value = (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ");

        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
    }
}