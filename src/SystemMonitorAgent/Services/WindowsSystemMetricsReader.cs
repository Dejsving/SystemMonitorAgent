using System.Runtime.InteropServices;
using SystemMonitor.Shared.Models;

namespace SystemMonitorAgent.Services;

public sealed class WindowsSystemMetricsReader : ISystemMetricsReader
{
    public async Task<double?> GetCpuUsagePercentAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        CpuSample first = ReadWindowsCpuSample();
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        CpuSample second = ReadWindowsCpuSample();

        ulong idle = second.Idle - first.Idle;
        ulong total = second.Total - first.Total;

        return total == 0
            ? null
            : Math.Round((1.0 - (idle / (double)total)) * 100, 2);
    }

    public bool TryGetMemoryInfo(out MemoryInfo memoryInfo)
    {
        memoryInfo = null!;
        if (OperatingSystem.IsWindows() && TryGetWindowsMemoryStatus(out MemoryStatusEx memoryStatus))
        {
            memoryInfo = new MemoryInfo
            {
                TotalBytes = (long)memoryStatus.TotalPhysicalMemory,
                UsedBytes = (long)(memoryStatus.TotalPhysicalMemory - memoryStatus.AvailablePhysicalMemory)
            };
            return true;
        }

        return false;
    }

    private static CpuSample ReadWindowsCpuSample()
    {
        if (!GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime))
        {
            throw new InvalidOperationException("GetSystemTimes returned false.");
        }

        ulong idle = idleTime.ToUInt64();
        ulong kernel = kernelTime.ToUInt64();
        ulong user = userTime.ToUInt64();

        return new CpuSample(idle, kernel + user);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusExNative(out MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct CpuSample(ulong Idle, ulong Total);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct FileTime
    {
        public uint LowDateTime { get; init; }

        public uint HighDateTime { get; init; }

        public ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysicalMemory;
        public ulong AvailablePhysicalMemory;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private static bool TryGetWindowsMemoryStatus(out MemoryStatusEx buffer)
    {
        buffer = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusExNative(out buffer);
    }
}
