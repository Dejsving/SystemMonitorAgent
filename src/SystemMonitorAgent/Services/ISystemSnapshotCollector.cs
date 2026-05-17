using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Shared.Models;

namespace SystemMonitorAgent.Services;

public interface ISystemSnapshotCollector
{
    Task<AgentPayload> CollectAsync(IReadOnlyCollection<string> requiredProcesses, CancellationToken cancellationToken);
}
