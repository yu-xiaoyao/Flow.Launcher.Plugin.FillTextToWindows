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
        var keyDelayMs = item.KeyDelayMs;
        var pasteDelayMs = item.PasteDelayMs;
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
        }

        // 开始按行复制粘贴数据。
        // 行上的按键（数据行配置模式）和主表的按键是叠加关系：
        //   开始前按键：主表发完接着发这一行的，整批只在第一个粘贴之前发一次；
        //   粘贴后按键：非空就顶掉主表的，空的话继续用主表的；
        //   最后一段之后按键：发在这个循环之后、主表的最后一段之后按键之前。
        for (var i = 0; i < values.Count; i++)
        {
            var lineItem = values[i];
            if (i == 0)
            {
                // 发送前执行按键
                success = await SendKeys(metadata, lineItem.ItemLeadingKeys, keyDelayMs);
                if (!success) return;
            }

            // 开始复制/粘贴
            success = await FillText(metadata, lineItem.TextData, keyDelayMs, pasteDelayMs);
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

    private static async Task<bool> FillText(FillTextTaskMetadata metadata, string textData, int keyDelayMs,
        int pastedDelayMs)
    {
        InnerLogger.Logger.Trace($"开始填充Text. textData: {textData}");

        if (!ClipboardHelper.SetText(textData))
        {
            InnerLogger.Logger.Warn("写入剪贴板失败（可能被其它程序占用），已停止填充。");
            return false;
        }

        await WaitMills(metadata, pastedDelayMs);

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
    /// 开了「数据行配置模式」（<see cref="FillEntry.UseLineSettings"/>）时，每一段带上自己的按键；
    /// 没开就都是空数组，执行时自动回落成主表那一套。
    /// </para>
    /// </summary>
    public static FillTextItem ToFillTextItem(FillEntry entry, Settings settings)
    {
        var useLineSettings = entry?.UseLineSettings ?? false;
        var lines = entry?.Values ?? new List<FillEntryLine>();

        var values = lines.Select(line => new FillTextLineItem
        {
            TextData = line.Value,
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

    private static FillTextItem BuildFillTextItem(Settings settings, IReadOnlyList<FillTextLineItem> values)
    {
        return new FillTextItem
        {
            BeforeFillDelayMs = 0,
            KeyDelayMs = settings.KeyDelayMs,
            PasteDelayMs = settings.PasteDelayMs,
            RestoreClipboard = settings.RestoreClipboard,
            LeadingKeys = settings.LeadingKeys,
            NextFieldKeys = settings.NextFieldKeys,
            LastFieldKeys = settings.LastFieldKeys,
            Values = values,
        };
    }
}