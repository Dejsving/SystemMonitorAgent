using SystemMonitorAgent;
using SystemMonitorAgent.Logging;
using SystemMonitorAgent.Services;
using SystemMonitorAgent.Configuration;
using SystemMonitorAgent.Health;
using SystemMonitorAgent.BackgroundTasks;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

var configurationWarnings = new List<string>();
IConfigurationRoot configurationRoot;

try
{
    configurationRoot = new ConfigurationBuilder()
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables(prefix: "SYSTEMMONITORAGENT_")
        .Build();
}
catch (Exception exception)
{
    configurationWarnings.Add($"Failed to read configuration files. Default settings will be used. {exception.Message}");
    configurationRoot = new ConfigurationBuilder()
        .AddEnvironmentVariables(prefix: "SYSTEMMONITORAGENT_")
        .Build();
}

builder.Configuration.AddConfiguration(configurationRoot);

builder.Services.AddOptions<AgentOptions>()
    .BindConfiguration(AgentOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.PostConfigure<AgentOptions>(options =>
{
    if (!string.IsNullOrWhiteSpace(options.LogFilePath) && !Path.IsPathRooted(options.LogFilePath))
    {
        options.LogFilePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.LogFilePath));
    }
    
    options.RequiredProcesses = (options.RequiredProcesses ?? [])
        .Where(static processName => !string.IsNullOrWhiteSpace(processName))
        .Select(static processName => processName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static processName => processName, StringComparer.OrdinalIgnoreCase)
        .ToArray();
});

var tempOptions = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<ISystemMetricsReader, WindowsSystemMetricsReader>();
}
builder.Services.AddSingleton<ISystemSnapshotCollector, SystemSnapshotCollector>();
builder.Services.AddSingleton<PayloadQueue>();
builder.Services.AddSingleton<AgentHealthState>();
builder.Services.AddHostedService<Worker>();

var isConsoleMode = args.Any(arg => arg == "--console");

if (OperatingSystem.IsWindows() && !isConsoleMode)
{
    builder.Services.AddWindowsService(serviceOptions =>
    {
        serviceOptions.ServiceName = "SystemMonitorAgent";
    });
}

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Information);

if (OperatingSystem.IsWindows() && !isConsoleMode)
{
#pragma warning disable CA1416
    builder.Logging.AddEventLog(options =>
    {
        options.SourceName = "SystemMonitorAgent";
    });
#pragma warning restore CA1416
    // Пишем в Event Log всё, начиная с заданного минимального уровня
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(level => level >= tempOptions.EventLogLevel);
}

if (isConsoleMode)
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });
}

builder.Logging.AddProvider(new FileLoggerProvider(tempOptions.LogFilePath));

builder.Services.AddHostedService<HealthcheckService>();

var host = builder.Build();

await host.RunAsync();
