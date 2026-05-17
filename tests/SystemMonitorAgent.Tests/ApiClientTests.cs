using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SystemMonitor.Shared.Models;
using Xunit;
using SystemMonitorAgent.Configuration;
using SystemMonitorAgent.Services;

namespace SystemMonitorAgent.Tests;

public class ApiClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ApiClient>> _mockLogger;
    private readonly AgentOptions _options;
    private readonly ApiClient _apiClient;

    public ApiClientTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object);
        _mockLogger = new Mock<ILogger<ApiClient>>();
        
        _options = new AgentOptions
        {
            ApiUrl = "http://localhost/api/payload",
            HttpTimeoutSeconds = 5
        };

        var healthState = new SystemMonitorAgent.Health.AgentHealthState();
        _apiClient = new ApiClient(_httpClient, Options.Create(_options), _mockLogger.Object, healthState);
    }

    [Fact]
    public async Task SendAsync_ReturnsTrue_OnSuccess()
    {
        // Arrange
        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var payload = new AgentPayload();

        // Act
        var result = await _apiClient.SendAsync(payload, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_ReturnsFalse_OnErrorResponse(HttpStatusCode statusCode)
    {
        // Arrange
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("Error message")
        };

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var payload = new AgentPayload();

        // Act
        var result = await _apiClient.SendAsync(payload, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        
        // Check that an error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send data to API")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_ReturnsFalse_OnHttpTimeout()
    {
        // Arrange
        var tcs = new TaskCompletionSource<HttpResponseMessage>();
        tcs.SetCanceled(); // Simulate OperationCanceledException for timeout

        _mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var payload = new AgentPayload();

        // Act
        var result = await _apiClient.SendAsync(payload, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);

        // Check that the timeout error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<OperationCanceledException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
