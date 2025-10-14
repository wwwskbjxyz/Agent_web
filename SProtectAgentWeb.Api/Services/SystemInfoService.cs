using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SProtectAgentWeb.Api.Dtos;

namespace SProtectAgentWeb.Api.Services;

/// <summary>
/// 提供服务器运行状态信息。
/// </summary>
public class SystemInfoService
{
    public Task<SystemStatusResponse> GetStatusAsync()
    {
        var status = new SystemStatusResponse
        {
            MachineName = Environment.MachineName,
            OsDescription = RuntimeInformation.OSDescription,
            ServerTime = DateTimeOffset.Now
        };

        if (OperatingSystem.IsWindows())
        {
            PopulateWindowsMetrics(status);
        }
        else
        {
            PopulateCrossPlatformMetrics(status);
        }

        if (status.TotalMemoryBytes.HasValue && status.FreeMemoryBytes.HasValue)
        {
            var total = status.TotalMemoryBytes.Value;
            var free = status.FreeMemoryBytes.Value;
            status.UsedMemoryBytes = total >= free ? total - free : (ulong?)null;
            if (total > 0)
            {
                status.MemoryUsagePercentage = Math.Round((double)(total - free) / total * 100, 2);
            }
        }
        else if (status.TotalMemoryBytes.HasValue && status.UsedMemoryBytes.HasValue)
        {
            var total = status.TotalMemoryBytes.Value;
            var used = status.UsedMemoryBytes.Value;
            status.FreeMemoryBytes = total >= used ? total - used : (ulong?)null;
            if (total > 0)
            {
                status.MemoryUsagePercentage = Math.Round((double)used / total * 100, 2);
            }
        }

        if (status.BootTime.HasValue)
        {
            status.UptimeSeconds = Math.Max(0d, (status.ServerTime - status.BootTime.Value).TotalSeconds);
        }

        return Task.FromResult(status);
    }

    private static void PopulateWindowsMetrics(SystemStatusResponse status)
    {
        try
        {
            using var osSearcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory,LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var obj in osSearcher.Get().OfType<ManagementObject>())
            {
                var totalKb = Convert.ToUInt64(obj["TotalVisibleMemorySize"] ?? 0UL);
                var freeKb = Convert.ToUInt64(obj["FreePhysicalMemory"] ?? 0UL);
                status.TotalMemoryBytes = totalKb * 1024UL;
                status.FreeMemoryBytes = freeKb * 1024UL;
                if (obj["LastBootUpTime"] is string bootRaw && !string.IsNullOrWhiteSpace(bootRaw))
                {
                    var boot = ManagementDateTimeConverter.ToDateTime(bootRaw);
                    status.BootTime = DateTime.SpecifyKind(boot, DateTimeKind.Local);
                }
                break;
            }
        }
        catch (Exception ex)
        {
            status.Warnings.Add($"读取内存信息失败: {ex.Message}");
        }

        try
        {
            using var cpuSearcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            var loads = new List<double>();
            foreach (var obj in cpuSearcher.Get().OfType<ManagementObject>())
            {
                if (obj["LoadPercentage"] is null) continue;
                if (double.TryParse(obj["LoadPercentage"].ToString(), out var load))
                {
                    loads.Add(load);
                }
            }
            if (loads.Count > 0)
            {
                status.CpuLoadPercentage = Math.Round(loads.Average(), 1);
            }
        }
        catch (Exception ex)
        {
            status.Warnings.Add($"读取CPU负载失败: {ex.Message}");
        }

        if (!status.BootTime.HasValue)
        {
            try
            {
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                status.BootTime = status.ServerTime - uptime;
            }
            catch (Exception ex)
            {
                status.Warnings.Add($"估算开机时间失败: {ex.Message}");
            }
        }
    }

    private static void PopulateCrossPlatformMetrics(SystemStatusResponse status)
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            if (gcInfo.TotalAvailableMemoryBytes > 0)
            {
                status.TotalMemoryBytes = (ulong)gcInfo.TotalAvailableMemoryBytes;
            }
        }
        catch (Exception ex)
        {
            status.Warnings.Add($"读取内存信息失败: {ex.Message}");
        }

        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            status.BootTime = status.ServerTime - uptime;
        }
        catch (Exception ex)
        {
            status.Warnings.Add($"估算开机时间失败: {ex.Message}");
        }
    }
}
