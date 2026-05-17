using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using SystemMonitor.Shared.Models;

using SystemMonitorAgent.Configuration;
using SystemMonitorAgent.Health;

namespace SystemMonitorAgent.Services;

public sealed class SystemSnapshotCollector : ISystemSnapshotCollector
{
    private readonly ILogger<SystemSnapshotCollector> _logger;
    private readonly ISystemMetricsReader? _metricsReader;

    public SystemSnapshotCollector(ILogger<SystemSnapshotCollector> logger, IEnumerable<ISystemMetricsReader> metricsReaders)
    {
        _logger = logger;
        _metricsReader = metricsReaders.FirstOrDefault();
    }

    public async Task<AgentPayload> CollectAsync(IReadOnlyCollection<string> requiredProcesses, CancellationToken cancellationToken)
    {
        string hostName = Dns.GetHostName();
        RunningProcessInfo[] runningProcesses = GetRunningProcesses();
        HashSet<string> runningProcessNames = runningProcesses
            .Select(static process => process.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new AgentPayload
        {
            CollectedAtUtc = DateTimeOffset.UtcNow,
            Hostname = hostName,
            IpAddresses = GetIpAddresses(hostName),
            WindowsVersion = RuntimeInformation.OSDescription,
            UptimeSeconds = (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds,
            CpuUsagePercent = await GetCpuUsagePercentAsync(cancellationToken),
            Memory = GetMemoryInfo(),
            Disks = GetDisks(),
            RunningProcesses = runningProcesses,
            RequiredProcesses = requiredProcesses
                .Select(processName => new RequiredProcessInfo
                {
                    Name = processName,
                    IsRunning = runningProcessNames.Contains(processName)
                })
                .ToArray()
        };
    }

    private string[] GetIpAddresses(string hostName)
    {
        try
        {
            return Dns.GetHostAddresses(hostName)
                .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                .Where(address => !IPAddress.IsLoopback(address))
                .Select(static address => address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static address => address, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to resolve IP addresses for host {HostName}.", hostName);
            return [];
        }
    }

    private async Task<double?> GetCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (OperatingSystem.IsWindows() && _metricsReader != null)
            {
                return await _metricsReader.GetCpuUsagePercentAsync(cancellationToken);
            }
            
            return OperatingSystem.IsLinux()
                    ? await GetLinuxCpuUsagePercentAsync(cancellationToken)
                    : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to collect CPU usage.");
            return null;
        }
    }

    private static async Task<double?> GetLinuxCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        CpuSample first = ReadLinuxCpuSample();
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        CpuSample second = ReadLinuxCpuSample();

        ulong idle = second.Idle - first.Idle;
        ulong total = second.Total - first.Total;

        return total == 0
            ? null
            : Math.Round((1.0 - (idle / (double)total)) * 100, 2);
    }

    private static CpuSample ReadLinuxCpuSample()
    {
        string? line = File.ReadLines("/proc/stat").FirstOrDefault(static value => value.StartsWith("cpu ", StringComparison.Ordinal));
        if (line is null)
        {
            throw new InvalidOperationException("The /proc/stat file does not contain aggregate CPU data.");
        }

        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        ulong[] values = parts
            .Skip(1)
            .Select(value => ulong.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();

        ulong idle = values.Length > 4
            ? values[3] + values[4]
            : values[3];
        ulong total = values.Aggregate(0UL, static (sum, next) => sum + next);

        return new CpuSample(idle, total);
    }

    private MemoryInfo GetMemoryInfo()
    {
        if (OperatingSystem.IsWindows() && _metricsReader != null && _metricsReader.TryGetMemoryInfo(out MemoryInfo windowsMemoryInfo))
        {
            return windowsMemoryInfo;
        }

        if (OperatingSystem.IsLinux())
        {
            Dictionary<string, long> values = File.ReadLines("/proc/meminfo")
                .Select(static line => line.Split(':', 2, StringSplitOptions.TrimEntries))
                .Where(static parts => parts.Length == 2)
                .ToDictionary(
                    static parts => parts[0],
                    static parts =>
                    {
                        string value = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                        return long.Parse(value, CultureInfo.InvariantCulture) * 1024;
                    },
                    StringComparer.OrdinalIgnoreCase);

            long totalBytes = values.GetValueOrDefault("MemTotal");
            long availableBytes = values.GetValueOrDefault("MemAvailable");

            return new MemoryInfo
            {
                TotalBytes = totalBytes,
                UsedBytes = Math.Max(0, totalBytes - availableBytes)
            };
        }

        long fallbackTotalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return new MemoryInfo
        {
            TotalBytes = fallbackTotalBytes > 0 ? fallbackTotalBytes : 0,
            UsedBytes = GC.GetTotalMemory(forceFullCollection: false)
        };
    }

    private DiskInfo[] GetDisks()
    {
        List<DiskInfo> disks = [];

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                disks.Add(new DiskInfo
                {
                    Name = drive.Name,
                    AvailableFreeSpaceBytes = drive.AvailableFreeSpace,
                    TotalSizeBytes = drive.TotalSize
                });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to read information about drive {DriveName}.", drive.Name);
            }
        }

        return disks
            .OrderBy(static disk => disk.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private RunningProcessInfo[] GetRunningProcesses()
    {
        List<RunningProcessInfo> processes = [];

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                processes.Add(new RunningProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName
                });
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to inspect one of the running processes.");
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes
            .OrderBy(static process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static process => process.Id)
            .ToArray();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CpuSample(ulong Idle, ulong Total);
}
