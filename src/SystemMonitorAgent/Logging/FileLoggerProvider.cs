using System.Collections.Concurrent;

namespace SystemMonitorAgent.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private readonly string _filePath;
    private StreamWriter? _writer;
    private string _currentDate = string.Empty;

    public FileLoggerProvider(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine("logs", "agent", "system-monitor-agent.log")
            : filePath;
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, WriteMessage));

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
        }
    }

    private void WriteMessage(
        string categoryName,
        LogLevel logLevel,
        string message,
        Exception? exception)
    {
        try
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            string fullPath = Path.GetFullPath(_filePath);
            string? directoryPath = Path.GetDirectoryName(fullPath);

            lock (_sync)
            {
                if (_writer != null && _currentDate != today)
                {
                    _writer.Dispose();
                    _writer = null;
                }

                if (_writer == null)
                {
                    if (!string.IsNullOrWhiteSpace(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
                    string extension = Path.GetExtension(fullPath);
                    string todayLogPath = Path.Combine(directoryPath ?? string.Empty, $"{fileNameWithoutExt}-{today}{extension}");

                    _writer = new StreamWriter(new FileStream(todayLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        AutoFlush = true
                    };
                    _currentDate = today;
                }

                _writer.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {categoryName}: {message}");

                if (exception is not null)
                {
                    _writer.WriteLine(exception);
                }
            }
        }
        catch
        {
            // Logging failures must not bring down the service.
        }
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly Action<string, LogLevel, string, Exception?> _writeMessage;
    private readonly string _categoryName;

    public FileLogger(string categoryName, Action<string, LogLevel, string, Exception?> writeMessage)
    {
        _categoryName = categoryName;
        _writeMessage = writeMessage;
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        _writeMessage(_categoryName, logLevel, message, exception);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
