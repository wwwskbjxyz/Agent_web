using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Native;

namespace SProtectAgentWeb.Api.Database;

/// <summary>
/// Centralised database manager that mirrors the behaviour of the Go implementation.
/// It resolves database paths, ensures files are writable and delegates SQL operations to the native bridge.
/// </summary>
public class DatabaseManager
{
    private static readonly ConcurrentDictionary<string, byte> JournalDeletionFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppConfig _config;
    private readonly ILogger<DatabaseManager> _logger;
    public DatabaseManager(AppConfig config, ILogger<DatabaseManager> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <summary>
    /// Resolves the physical SQLite database path for the specified software and ensures it is writable.
    /// This allows native helpers to operate on the file directly without exposing SQL logic in managed code.
    /// </summary>
    public async Task<string> PrepareDatabasePathAsync(string software)
    {
        string fileName;
        if (string.IsNullOrWhiteSpace(software) || string.Equals(software, "默认软件", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "idc.db";
        }
        else
        {
            fileName = $"idc_{software}.db";
        }

        var resolvedPath = ResolveDatabasePath(fileName);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"Database file not found: {resolvedPath}");
        }

        await EnsureWritableAsync(resolvedPath).ConfigureAwait(false);
        RemoveJournalArtifacts(resolvedPath);
        return resolvedPath;
    }

    /// <summary>
    /// Lists all enabled softwares from the primary database using the native bridge implementation.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetEnabledSoftwaresAsync()
    {
        var databasePath = await PrepareDatabasePathAsync("默认软件").ConfigureAwait(false);
        return await Task.Run(
                () => SqliteBridge.GetMultiSoftwareRecords(databasePath)
                    .Where(record => record.State == 1)
                    .Select(record => record.SoftwareName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList())
            .ConfigureAwait(false);
    }

    private string ResolveDatabasePath(string fileName)
    {
        var dataPath = _config.GetDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new InvalidOperationException("Database path is not configured. Please update appsettings.json");
        }

        var combined = Path.Combine(dataPath, fileName);
        return Path.GetFullPath(combined);
    }

    private Task EnsureWritableAsync(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                var updated = attributes & ~FileAttributes.ReadOnly;
                File.SetAttributes(path, updated);
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var dirAttributes = File.GetAttributes(directory);
                if ((dirAttributes & FileAttributes.ReadOnly) != 0)
                {
                    var updated = dirAttributes & ~FileAttributes.ReadOnly;
                    File.SetAttributes(directory, updated);
                }
            }
        }
        catch (Exception ex)
        {
            // Removing the read-only attribute can fail on some file systems (e.g. network shares).
            // Log the warning so administrators can adjust permissions manually.
            _logger?.LogWarning(ex, "Unable to clear read-only attribute for {DatabasePath}", path);
        }

        return Task.CompletedTask;
    }

    private void RemoveJournalArtifacts(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return;
        }

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var candidate = databasePath + suffix;
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                File.Delete(candidate);
                JournalDeletionFailures.TryRemove(candidate, out _);
            }
            catch (IOException ioEx) when (IsSharingViolation(ioEx))
            {
                // Another process is currently using the journal. Skip deletion silently to avoid noisy warnings.
                JournalDeletionFailures.TryAdd(candidate, 0);
            }
            catch (Exception ex)
            {
                if (JournalDeletionFailures.TryAdd(candidate, 0))
                {
                    _logger?.LogWarning(ex, "Unable to remove SQLite journal file {JournalFile}", candidate);
                }
            }
        }
    }

    private static bool IsSharingViolation(IOException ex)
    {
        const int SharingViolationHResult = unchecked((int)0x80070020);
        return ex.HResult == SharingViolationHResult;
    }

}

