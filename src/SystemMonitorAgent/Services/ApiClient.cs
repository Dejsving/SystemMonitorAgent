using System.Net.Http.Json;
using System.Text;
using SystemMonitor.Shared.Models;
using Microsoft.Extensions.Options;

using SystemMonitorAgent.Configuration;
using SystemMonitorAgent.Health;

namespace SystemMonitorAgent.Services;

public sealed class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly AgentOptions _options;
    private readonly ILogger<ApiClient> _logger;
    private readonly AgentHealthState _healthState;

    public ApiClient(HttpClient httpClient, IOptions<AgentOptions> options, ILogger<ApiClient> logger, AgentHealthState healthState)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _healthState = healthState;
    }

    public async Task<SendResult> SendAsync(AgentPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.HttpTimeoutSeconds));

            using HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync(_options.ApiUrl, payload, timeoutSource.Token);

            if (response.IsSuccessStatusCode)
            {
                _healthState.LastSuccessfulSendTime = DateTimeOffset.UtcNow;
                _healthState.LastErrorMessage = null;
                return new SendResult(true, (int)response.StatusCode, null);
            }

            string responseBody = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            string errorMsg = $"Status code: {(int)response.StatusCode}. Response: {responseBody}";
            _healthState.LastErrorMessage = errorMsg;
            _logger.LogError(
                "Failed to send data to API. Status code: {StatusCode}. Response: {ResponseBody}",
                (int)response.StatusCode,
                responseBody);
            
            return new SendResult(false, (int)response.StatusCode, errorMsg);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            string errorMsg = $"Request timed out after {_options.HttpTimeoutSeconds} seconds.";
            _healthState.LastErrorMessage = errorMsg;
            _logger.LogError(exception, "HTTP request to {ApiUrl} timed out after {TimeoutSeconds} seconds.", _options.ApiUrl, _options.HttpTimeoutSeconds);
            return new SendResult(false, 408, errorMsg); // 408 Request Timeout
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            _healthState.LastErrorMessage = exception.Message;
            _logger.LogError(exception, "HTTP request error to {ApiUrl}.", _options.ApiUrl);
            return new SendResult(false, exception.StatusCode.HasValue ? (int)exception.StatusCode.Value : 0, exception.Message);
        }
        catch (Exception exception)
        {
            _healthState.LastErrorMessage = exception.Message;
            _logger.LogError(exception, "Unable to send data to API {ApiUrl}.", _options.ApiUrl);
            return new SendResult(false, 0, exception.Message);
        }
    }
}
