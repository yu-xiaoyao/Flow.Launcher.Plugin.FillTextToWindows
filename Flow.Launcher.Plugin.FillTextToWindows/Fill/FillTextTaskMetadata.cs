using System;
using System.Threading;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

public record FillTextTaskMetadata(FillTextItem Item)
{
    private int _started;

    public DateTime StartTime { get; private set; } = DateTime.Now;
    public string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 认领这次填充。窗口隐藏时 <c>VisibilityChanged</c> 可能连着触发好几次，
    /// 同一个任务只让第一个后台线程跑，否则会并发粘贴、内容重复。
    /// </summary>
    public bool TryStart()
    {
        return Interlocked.CompareExchange(ref _started, 1, 0) == 0;
    }
}