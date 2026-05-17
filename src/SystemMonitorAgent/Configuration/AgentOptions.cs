using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace SystemMonitorAgent.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    [Required]
    [Url]
    public string ApiUrl { get; set; } = "http://localhost:5000/api/system-monitor";

    [Range(1, int.MaxValue)]
    public int CollectionIntervalSeconds { get; set; } = 30;

    public string[] RequiredProcesses { get; set; } = [];

    [Required]
    public string LogFilePath { get; set; } = Path.Combine("logs", "agent", "system-monitor-agent.log");

    public LogLevel EventLogLevel { get; set; } = LogLevel.Information;

    [Range(1, int.MaxValue)]
    public int HttpTimeoutSeconds { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int RetryQueueMaxItems { get; set; } = 1000;
}
