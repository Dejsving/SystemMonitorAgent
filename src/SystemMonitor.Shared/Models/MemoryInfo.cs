namespace SystemMonitor.Shared.Models;

public sealed class MemoryInfo
{
    public long TotalBytes { get; init; }

    public long UsedBytes { get; init; }
}
