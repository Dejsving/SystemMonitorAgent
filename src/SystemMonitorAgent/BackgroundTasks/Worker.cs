using SystemMonitor.Shared.Models;
using Microsoft.Extensions.Options;

using SystemMonitorAgent.Services;
using SystemMonitorAgent.Configuration;

namespace SystemMonitorAgent.BackgroundTasks;

public sealed class Worker : BackgroundService
{
    private readonly IApiClient _apiClient;
    private readonly ISystemSnapshotCollector _collector;
    private readonly PayloadQueue _payloadQueue;
    private readonly ILogger<Worker> _logger;
    private readonly AgentOptions _options;

    public Worker(
        IApiClient apiClient,
        ISystemSnapshotCollector collector,
        PayloadQueue payloadQueue,
        IOptions<AgentOptions> options,
        ILogger<Worker> logger)
    {
        _apiClient = apiClient;
        _collector = collector;
        _payloadQueue = payloadQueue;
        _logger = logger;
        _options = options.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("System Monitor Agent service is starting.");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("System Monitor Agent service is stopping.");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(_options.CollectionIntervalSeconds));

        try
        {
            do
            {
                try
                {
                    await FlushRetryQueueAsync(stoppingToken);

                    AgentPayload payload = await _collector.CollectAsync(_options.RequiredProcesses, stoppingToken);

                    var sendResult = await _apiClient.SendAsync(payload, stoppingToken);

                    if (sendResult.IsSuccess)
                    {
                        _logger.LogInformation("System information was sent successfully to {ApiUrl}.", _options.ApiUrl);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send data ({StatusCode}: {ErrorMessage}). Adding payload to the retry queue.", sendResult.StatusCode, sendResult.ErrorMessage);
                        await _payloadQueue.EnqueueAsync(payload, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Ignore, let the outer catch handle it if needed, or loop condition will stop it if we use break
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unexpected error during data collection or delivery.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
            _logger.LogInformation("Worker loop cancelled gracefully.");
        }
    }

    private async Task FlushRetryQueueAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && _payloadQueue.TryDequeue(out var payload))
        {
            var sendResult = await _apiClient.SendAsync(payload!, stoppingToken);

            if (sendResult.IsSuccess)
            {
                _logger.LogInformation("Successfully sent buffered payload data.");
            }
            else
            {
                _logger.LogWarning("Retry failed ({StatusCode}: {ErrorMessage}). Re-enqueueing and stopping flush.", sendResult.StatusCode, sendResult.ErrorMessage);
                await _payloadQueue.EnqueueAsync(payload!, stoppingToken);
                break;
            }
        }
    }
}
