using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SystemMonitor.Shared.Models;
using Xunit;
using SystemMonitorAgent.Configuration;
using SystemMonitorAgent.Health;
using SystemMonitorAgent.Services;
using SystemMonitorAgent.BackgroundTasks;

namespace SystemMonitorAgent.Tests;

public class WorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ShutsDownGracefully_OnCancellation()
    {
        // Arrange
        var mockApiClientLogger = new Mock<ILogger<ApiClient>>();
        var mockCollectorLogger = new Mock<ILogger<SystemSnapshotCollector>>();
        var mockWorkerLogger = new Mock<ILogger<Worker>>();

        var options = new AgentOptions
        {
            CollectionIntervalSeconds = 5,
            ApiUrl = "http://localhost/test",
            RequiredProcesses = Array.Empty<string>()
        };

        var httpClient = new HttpClient();
        var apiClient = new ApiClient(httpClient, Options.Create(options), mockApiClientLogger.Object, new AgentHealthState());
        var collector = new SystemSnapshotCollector(mockCollectorLogger.Object, Array.Empty<ISystemMetricsReader>());
        var payloadQueue = new PayloadQueue(Options.Create(options));

        var worker = new Worker(apiClient, collector, payloadQueue, Options.Create(options), mockWorkerLogger.Object);
        using var cts = new CancellationTokenSource();

        // Act
        // Start the worker
        await worker.StartAsync(CancellationToken.None);
        
        // Call StopAsync to trigger graceful shutdown (it internally cancels the stoppingToken)
        await worker.StopAsync(CancellationToken.None);

        // Since it is a BackgroundService, we'll wait for the ExecuteTask to finish
        // If ExecuteAsync throws an unhandled exception, it will fail the test when awaited.
        Task? executeTask = worker.ExecuteTask;
        if (executeTask != null)
        {
            await executeTask; // Should complete gracefully without throwing
        }

        // Assert
        Assert.True(executeTask?.IsCompletedSuccessfully ?? true, "Worker should shutdown gracefully without errors");
        
        mockWorkerLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never, "Expected no unexpected errors logged during shutdown");
    }
}
