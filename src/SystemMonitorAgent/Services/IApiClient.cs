using System.Threading;
using System.Threading.Tasks;
using SystemMonitor.Shared.Models;

namespace SystemMonitorAgent.Services;

public interface IApiClient
{
    Task<SendResult> SendAsync(AgentPayload payload, CancellationToken cancellationToken);
}
