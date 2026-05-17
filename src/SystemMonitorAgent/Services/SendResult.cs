namespace SystemMonitorAgent.Services;

public record SendResult(bool IsSuccess, int StatusCode, string? ErrorMessage);
