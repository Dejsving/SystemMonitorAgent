using Newtonsoft.Json;
using NLog;
using NLog.Web;
using SystemMonitor.Shared.Models;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
logger.Debug("init main");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    string listenUrl = builder.Configuration["Receiver:ListenUrl"] ?? "http://localhost:5000";
    builder.WebHost.UseUrls(listenUrl);

    var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Service = "SystemMonitorReceiver",
    Status = "Running",
    ListenUrl = listenUrl
}));

app.MapPost("/api/system-monitor", (AgentPayload payload, ILogger<Program> logger) =>
{
    logger.LogInformation(
        "Payload received: Hostname={Hostname}, CollectedAtUtc={CollectedAtUtc}, RequiredProcesses={RequiredProcessesCount}, RunningProcesses={RunningProcessesCount}",
        payload.Hostname,
        payload.CollectedAtUtc,
        payload.RequiredProcesses.Length,
        payload.RunningProcesses.Length);

    string json = JsonConvert.SerializeObject(payload, Formatting.Indented);

    logger.LogInformation("==== RECEIVED PAYLOAD START ====\n{JsonPayload}\n==== RECEIVED PAYLOAD END ====", json);

    return Results.Ok(new { Message = "Payload accepted." });
});

app.Run();
}
catch (Exception exception)
{
    logger.Error(exception, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}
