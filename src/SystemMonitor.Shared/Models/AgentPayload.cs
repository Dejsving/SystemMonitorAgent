namespace SystemMonitor.Shared.Models;

public sealed class AgentPayload
{
    public DateTimeOffset CollectedAtUtc { get; init; }

    public string Hostname { get; init; } = string.Empty;

    public string[] IpAddresses { get; init; } = [];

    public string WindowsVersion { get; init; } = string.Empty;

    public long UptimeSeconds { get; init; }

    public double? CpuUsagePercent { get; init; }

    public MemoryInfo Memory { get; init; } = new();

    public DiskInfo[] Disks { get; init; } = [];

    public RunningProcessInfo[] RunningProcesses { get; init; } = [];

    public RequiredProcessInfo[] RequiredProcesses { get; init; } = [];
}
