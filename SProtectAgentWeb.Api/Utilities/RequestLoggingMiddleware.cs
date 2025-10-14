using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SProtectAgentWeb.Api.Utilities;

public sealed class RequestLoggingMiddleware
{
    private const string LogFilePrefix = "api-";
    private const string LogFileExtension = ".txt";
    private static readonly SemaphoreSlim WriterLock = new(1, 1);
    private static DateTime _lastCleanupDate = DateTime.MinValue;

    private readonly RequestDelegate _next;
    private readonly string _logsDirectory;

    public RequestLoggingMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _logsDirectory = environment.ContentRootPath;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var request = context.Request;
        var startTime = DateTimeOffset.Now;

        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = DateTimeOffset.Now - startTime;
            var statusCode = context.Response?.StatusCode ?? 0;
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var logLine = string.Join('\t', new[]
            {
                startTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                request.Method,
                $"{request.Path}{request.QueryString}",
                statusCode.ToString(CultureInfo.InvariantCulture),
                $"{elapsed.TotalMilliseconds:F0}ms",
                remoteIp
            });

            await AppendLogLineAsync(logLine, startTime.Date);
        }
    }

    private async Task AppendLogLineAsync(string line, DateTime date)
    {
        var filePath = Path.Combine(_logsDirectory, $"{LogFilePrefix}{date:yyyyMMdd}{LogFileExtension}");
        await WriterLock.WaitAsync();
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync(line);

            if (_lastCleanupDate.Date != date)
            {
                CleanupOldLogs(date);
                _lastCleanupDate = date;
            }
        }
        finally
        {
            WriterLock.Release();
        }
    }

    private void CleanupOldLogs(DateTime currentDate)
    {
        var cutoff = currentDate.AddDays(-6); // keep current day + previous 6 days
        foreach (var file in Directory.EnumerateFiles(_logsDirectory, $"{LogFilePrefix}*{LogFileExtension}", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (name.Length != LogFilePrefix.Length + 8 + LogFileExtension.Length)
            {
                continue;
            }

            var datePart = name.Substring(LogFilePrefix.Length, 8);
            if (!DateTime.TryParseExact(datePart, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
            {
                continue;
            }

            if (fileDate < cutoff)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
