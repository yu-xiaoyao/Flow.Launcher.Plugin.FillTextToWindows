using System;

namespace Flow.Launcher.Plugin.FillTextToWindows.Fill;

public record FillTextTaskMetadata(FillTextItem Item)
{
    public DateTime StartTime { get; private set; } = DateTime.Now;
    public string Id { get; private set; } = Guid.NewGuid().ToString();
}