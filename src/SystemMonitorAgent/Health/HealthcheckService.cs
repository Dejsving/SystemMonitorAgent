using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SystemMonitorAgent.BackgroundTasks;

namespace SystemMonitorAgent.Health;

public sealed class HealthcheckService : BackgroundService
{
    private readonly AgentHealthState _healthState;
    private readonly PayloadQueue _queue;
    private readonly ILogger<HealthcheckService> _logger;
    private TcpListener? _listener;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HealthcheckService(
        AgentHealthState healthState,
        PayloadQueue queue,
        ILogger<HealthcheckService> logger)
    {
        _healthState = healthState;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new TcpListener(IPAddress.Any, 5055);
        _listener.Start();
        _logger.LogInformation("Healthcheck endpoint started on port 5055.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => HandleClientAsync(client, stoppingToken), CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
            _logger.LogInformation("Healthcheck endpoint stopped.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var ownedClient = client;
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        var isHealth = requestLine?.StartsWith("GET /health", StringComparison.OrdinalIgnoreCase) == true;
        
        var status = isHealth ? "200 OK" : "404 Not Found";
        
        var responseObj = new
        {
            status = _healthState.Status,
            queueLength = _queue.Count,
            lastSuccessfulSend = _healthState.LastSuccessfulSendTime,
            lastError = _healthState.LastErrorMessage
        };

        var body = isHealth
            ? JsonSerializer.Serialize(responseObj, _jsonOptions)
            : "{\"error\":\"not found\"}";
            
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var headers = string.Join("\r\n",
            $"HTTP/1.1 {status}",
            "Content-Type: application/json; charset=utf-8",
            $"Content-Length: {bodyBytes.Length}",
            "Connection: close",
            "\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }
}
