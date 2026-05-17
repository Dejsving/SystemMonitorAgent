using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SystemMonitorAgent;
using SystemMonitor.Shared.Models;
using SystemMonitorAgent.Services;

namespace SystemMonitorAgent.Tests;

public class SystemSnapshotCollectorTests
{
    private readonly Mock<ILogger<SystemSnapshotCollector>> _loggerMock;
    private readonly SystemSnapshotCollector _collector;

    public SystemSnapshotCollectorTests()
    {
        _loggerMock = new Mock<ILogger<SystemSnapshotCollector>>();
        _collector = new SystemSnapshotCollector(_loggerMock.Object, Array.Empty<ISystemMetricsReader>());
    }

    [Fact]
    public async Task CollectAsync_ShouldReturnValidPayload()
    {
        // Arrange
        var requiredProcesses = new List<string> { "Explorer", "NonExistentProc_12345" };
        var cancellationToken = CancellationToken.None;

        // Act
        var payload = await _collector.CollectAsync(requiredProcesses, cancellationToken);

        // Assert
        Assert.NotNull(payload);
        Assert.NotNull(payload.Hostname);
        Assert.NotEmpty(payload.IpAddresses); // Localhost should have at least one IP
        Assert.True(payload.UptimeSeconds > 0);
        
        Assert.NotNull(payload.Memory);
        Assert.True(payload.Memory.TotalBytes > 0);
        
        Assert.NotNull(payload.Disks);
        
        Assert.NotNull(payload.RunningProcesses);
        
        Assert.NotNull(payload.RequiredProcesses);
        Assert.Equal(2, payload.RequiredProcesses.Length);
        
        Assert.Contains(payload.RequiredProcesses, p => p.Name == "NonExistentProc_12345" && p.IsRunning == false);
    }
}
