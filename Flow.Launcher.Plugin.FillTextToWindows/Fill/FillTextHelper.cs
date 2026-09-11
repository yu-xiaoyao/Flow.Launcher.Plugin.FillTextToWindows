using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        // Timeout....
        if (DateTime.Now - metadata.StartTime <= TimeSpan.FromSeconds(10))
        {
            await StartFillTextAsync(metadata);
        }
    }

    public static async Task StartFillTextAsync(FillTextTaskMetadata metadata)
    {
        IDataObject clipboardOriginObject = null;
        if (metadata.Item.RestoreClipboard)
        {
            clipboardOriginObject = Clipboard.GetDataObject();
        }

        InnerLogger.Logger.Trace("开始填充任务");

        await DoStartFillTextAsync(metadata);

        // restore Clipboard Object
        if (clipboardOriginObject != null)
        {
            InnerLogger.Logger.Trace("填充结束. 恢复原始剪贴板数据");

            Clipboard.SetDataObject(clipboardOriginObject);
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

        // 开始按行复制粘贴数据
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