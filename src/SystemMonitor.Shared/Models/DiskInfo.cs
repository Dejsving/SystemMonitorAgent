namespace SystemMonitor.Shared.Models;

public sealed class DiskInfo
{
    public string Name { get; init; } = string.Empty;

    public long AvailableFreeSpaceBytes { get; init; }

    public long TotalSizeBytes { get; init; }
}
