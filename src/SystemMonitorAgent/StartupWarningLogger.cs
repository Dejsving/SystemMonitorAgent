namespace SystemMonitorAgent;

public sealed class StartupWarningLogger : IHostedService
{
    private readonly ILogger<StartupWarningLogger> _logger;
    private readonly IReadOnlyCollection<string> _warnings;

    public StartupWarningLogger(IReadOnlyCollection<string> warnings, ILogger<StartupWarningLogger> logger)
    {
        _warnings = warnings;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (string warning in _warnings)
        {
            _logger.LogWarning("{WarningMessage}", warning);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
