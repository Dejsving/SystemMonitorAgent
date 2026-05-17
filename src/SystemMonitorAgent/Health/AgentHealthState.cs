namespace SystemMonitorAgent.Health;

public class AgentHealthState
{
    public DateTimeOffset? LastSuccessfulSendTime { get; set; }
    public string? LastErrorMessage { get; set; }
    public string Status { get; set; } = "Running";
}
