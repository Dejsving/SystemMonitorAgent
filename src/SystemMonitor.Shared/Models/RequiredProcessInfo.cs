namespace SystemMonitor.Shared.Models;

public sealed class RequiredProcessInfo
{
    public string Name { get; init; } = string.Empty;

    public bool IsRunning { get; init; }
}
