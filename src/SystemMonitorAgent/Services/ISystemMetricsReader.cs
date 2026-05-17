using SystemMonitor.Shared.Models;

namespace SystemMonitorAgent.Services;

public interface ISystemMetricsReader
{
    Task<double?> GetCpuUsagePercentAsync(CancellationToken cancellationToken);
    bool TryGetMemoryInfo(out MemoryInfo memoryInfo);
}
