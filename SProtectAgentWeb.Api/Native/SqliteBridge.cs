using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SProtectAgentWeb.Api.Native;

internal static partial class SqliteBridge
{
    private const string LibraryName = "sp_sqlite_bridge";

    private static readonly string[] CandidateRelativeRoots =
    {
        string.Empty,
        Path.Combine("NativeBinaries"),
        Path.Combine("nativebinaries"),
        Path.Combine("Native", "Binaries"),
        Path.Combine("native", "binaries"),
        Path.Combine("Native", "sqlite_bridge"),
        Path.Combine("native", "sqlite_bridge"),
        Path.Combine("sqlite_bridge"),
        Path.Combine("Native", "sqlite_bridge", "bin"),
        Path.Combine("Native", "sqlite_bridge", "bin", "Debug"),
        Path.Combine("Native", "sqlite_bridge", "bin", "Release"),
        Path.Combine("native", "sqlite_bridge", "bin"),
        Path.Combine("native", "sqlite_bridge", "bin", "Debug"),
        Path.Combine("native", "sqlite_bridge", "bin", "Release"),
        Path.Combine("native", "sqlite_bridge", "build"),
        Path.Combine("native", "sqlite_bridge", "build", "Debug"),
        Path.Combine("native", "sqlite_bridge", "build", "Release"),
        Path.Combine("native", "sqlite_bridge", "x64", "Debug"),
        Path.Combine("native", "sqlite_bridge", "x64", "Release"),
        Path.Combine("native", "sqlite_bridge", "x86", "Debug"),
        Path.Combine("native", "sqlite_bridge", "x86", "Release"),
    };

    private static readonly object NativeInitializationLock = new();
    private static bool s_nativeInitializationAttempted;
    private static bool s_nativeAvailable;
    private static IntPtr s_nativeHandle;
    private static string? s_nativePath;
    private static ImmutableArray<string> s_lastProbePaths = ImmutableArray<string>.Empty;
    private static bool s_embeddedLibraryExtractionAttempted;
    private static string? s_embeddedLibraryPath;

    static SqliteBridge()
    {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ResolveNativeLibrary);
    }

    private static bool UseNativeLibrary
    {
        get
        {
            EnsureNativeAvailability();
            return s_nativeAvailable;
        }
    }

    internal static bool IsNativeAvailable => UseNativeLibrary;

    private static void DisableNativeLibrary()
    {
        lock (NativeInitializationLock)
        {
            if (s_nativeHandle != IntPtr.Zero)
            {
                try
                {
                    NativeLibrary.Free(s_nativeHandle);
                }
                catch
                {
                    // ignore failures while disposing the handle – the runtime
                    // will unload any partially loaded module once the process
                    // terminates and we only care about preventing further use.
                }

                s_nativeHandle = IntPtr.Zero;
            }

            s_nativeAvailable = false;
        }
    }

    private static void EnsureNativeLibraryIsAvailable()
    {
        EnsureNativeAvailability();

        if (!s_nativeAvailable)
        {
            var message = $"无法加载原生库 {GetPlatformLibraryFileName()}。";
            if (!s_lastProbePaths.IsDefaultOrEmpty)
            {
                message += " 已尝试路径: " + string.Join(", ", s_lastProbePaths);
            }

            throw new DllNotFoundException(message);
        }
    }

    private static T ExecuteNative<T>(Func<T> nativeInvoker)
    {
        EnsureNativeLibraryIsAvailable();
        return nativeInvoker();
    }

    private static void ExecuteNative(Action nativeInvoker)
    {
        EnsureNativeLibraryIsAvailable();
        nativeInvoker();
    }

    private static void EnsureNativeAvailability()
    {
        if (s_nativeInitializationAttempted)
        {
            return;
        }

        lock (NativeInitializationLock)
        {
            if (s_nativeInitializationAttempted)
            {
                return;
            }

            s_nativeInitializationAttempted = true;
            var fileName = GetPlatformLibraryFileName();
            var candidateList = EnumerateCandidatePaths(fileName).ToList();

            if (TryPrepareEmbeddedLibrary(fileName, out var embeddedPath))
            {
                candidateList.Add(embeddedPath);
            }

            s_lastProbePaths = candidateList.ToImmutableArray();

            foreach (var candidate in candidateList)
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (NativeLibrary.TryLoad(candidate, out var handle))
                {
                    s_nativePath = candidate;
                    s_nativeHandle = handle;
                    s_nativeAvailable = true;
                    return;
                }
            }

            if (NativeLibrary.TryLoad(LibraryName, out var fallbackHandle))
            {
                s_nativePath = LibraryName;
                s_nativeHandle = fallbackHandle;
                s_nativeAvailable = true;
                return;
            }

            s_nativeAvailable = false;
            s_nativeHandle = IntPtr.Zero;
            s_nativePath = null;
        }
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        EnsureNativeAvailability();

        if (!s_nativeAvailable)
        {
            return IntPtr.Zero;
        }

        if (s_nativeHandle != IntPtr.Zero)
        {
            return s_nativeHandle;
        }

        if (!string.IsNullOrEmpty(s_nativePath) && NativeLibrary.TryLoad(s_nativePath, out var handle))
        {
            s_nativeHandle = handle;
            return handle;
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out handle))
        {
            s_nativeHandle = handle;
            return handle;
        }

        s_nativeAvailable = false;
        s_nativeHandle = IntPtr.Zero;
        return IntPtr.Zero;
    }

    private static bool TryPrepareEmbeddedLibrary(string fileName, out string path)
    {
        if (s_embeddedLibraryExtractionAttempted)
        {
            path = s_embeddedLibraryPath ?? string.Empty;
            return s_embeddedLibraryPath is not null;
        }

        s_embeddedLibraryExtractionAttempted = true;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = SelectEmbeddedResourceName(assembly, fileName);

        if (resourceName is null)
        {
            path = string.Empty;
            return false;
        }

        var targetDirectory = Path.Combine(Path.GetTempPath(), "SProtectAgentWeb", "NativeBinaries");

        try
        {
            Directory.CreateDirectory(targetDirectory);
        }
        catch
        {
            path = string.Empty;
            return false;
        }

        path = Path.Combine(targetDirectory, fileName);

        try
        {
            using var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream is null)
            {
                path = string.Empty;
                return false;
            }

            using (var destination = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                resourceStream.CopyTo(destination);
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                try
                {
                    const UnixFileMode desiredPermissions =
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

                    File.SetUnixFileMode(path, desiredPermissions);
                }
                catch
                {
                    // Ignore failures – if the filesystem rejects chmod we will try to load the file as-is.
                }
            }
        }
        catch
        {
            path = string.Empty;
            return false;
        }

        s_embeddedLibraryPath = path;
        return true;
    }

    private static string? SelectEmbeddedResourceName(Assembly assembly, string fileName)
    {
        var candidates = assembly
            .GetManifestResourceNames()
            .Where(name => ResourceMatchesFileName(name, fileName))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return candidates.FirstOrDefault(c => ContainsOrdinalIgnoreCase(c, "win")) ?? candidates.First();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return candidates.FirstOrDefault(c => ContainsOrdinalIgnoreCase(c, "osx") || ContainsOrdinalIgnoreCase(c, "mac"))
                ?? candidates.First();
        }

        return candidates.FirstOrDefault(c => ContainsOrdinalIgnoreCase(c, "linux")) ?? candidates.First();
    }

    private static bool ResourceMatchesFileName(string resourceName, string fileName)
    {
        if (string.Equals(resourceName, fileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (resourceName.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (resourceName.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string fileName)
    {
        static IEnumerable<string> ExpandBase(string? baseDirectory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield break;
            }

            var absoluteBase = Path.GetFullPath(baseDirectory);

            foreach (var relativeRoot in CandidateRelativeRoots)
            {
                var candidate = string.IsNullOrEmpty(relativeRoot)
                    ? Path.Combine(absoluteBase, fileName)
                    : Path.Combine(absoluteBase, relativeRoot, fileName);

                yield return candidate;
            }

            // Support looking a couple of levels above the base directory (useful during local development).
            var parent = Directory.GetParent(absoluteBase);
            var levels = 0;
            while (parent is not null && levels < 3)
            {
                foreach (var relativeRoot in CandidateRelativeRoots)
                {
                    var candidate = string.IsNullOrEmpty(relativeRoot)
                        ? Path.Combine(parent.FullName, fileName)
                        : Path.Combine(parent.FullName, relativeRoot, fileName);

                    yield return candidate;
                }

                parent = parent.Parent;
                levels++;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandBase(AppContext.BaseDirectory, fileName))
        {
            var normalized = Path.GetFullPath(path);
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        foreach (var path in ExpandBase(assemblyDir, fileName))
        {
            var normalized = Path.GetFullPath(path);
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }

        foreach (var path in ExpandBase(Directory.GetCurrentDirectory(), fileName))
        {
            var normalized = Path.GetFullPath(path);
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static string GetPlatformLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "sp_sqlite_bridge.dll";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "libsp_sqlite_bridge.dylib";
        }

        return "libsp_sqlite_bridge.so";
    }

    internal readonly record struct UsageDistributionRecord(
        string Whom,
        string Payload,
        long ResolvedTotal,
        long UpdatedAt);

    internal readonly record struct MultiSoftwareRecord(
        string SoftwareName,
        int State,
        string Idc,
        int Version);

    internal readonly record struct IpLocationRecord(
        string Ip,
        string Province,
        string City,
        string District,
        long UpdatedAt);

    internal readonly record struct CardCreatorRecord(string Whom);

    internal readonly record struct CardIpRecord(string Value);

    internal readonly record struct BlacklistMachineRecord(
        string Value,
        int Type,
        string Remarks,
        long RowId);

    internal readonly record struct BlacklistLogRecord(
        long Id,
        string Ip,
        string Card,
        string PcSign,
        string ErrEvents,
        long Timestamp,
        long RowId);

    internal readonly record struct AgentRecord(
        string User,
        string Password,
        double AccountBalance,
        long AccountTime,
        string Duration,
        string Authority,
        string CardTypeAuthName,
        int CardsEnable,
        string Remarks,
        string FNode,
        int Stat,
        int DeletedAt,
        long DurationRaw,
        double Parities,
        double TotalParities);

    internal readonly record struct AgentStatisticsRecord(
        long TotalCards,
        long ActiveCards,
        long UsedCards,
        long ExpiredCards,
        long SubAgents);

    internal readonly record struct CardTypeRecord(
        string Name,
        string Prefix,
        int Duration,
        int Fyi,
        double Price,
        string Param,
        int Bind,
        int OpenNum,
        string Remarks,
        int CannotBeChanged,
        int AttrUnbindLimitTime,
        int AttrUnbindDeductTime,
        int AttrUnbindFreeCount,
        int AttrUnbindMaxCount,
        int BindIp,
        int BindMachineNum,
        int LockBindPcsign,
        long ActivateTime,
        long ExpiredTime,
        long LastLoginTime,
        int DelState,
        int Cty,
        long ExpiredTime2);

    internal readonly record struct CardRecord(
        string PrefixName,
        string Whom,
        string CardType,
        int Fyi,
        string State,
        int Bind,
        int OpenNum,
        int LoginCount,
        string Ip,
        string Remarks,
        long CreateData,
        long ActivateTime,
        long ExpiredTime,
        long LastLoginTime,
        int DelState,
        double Price,
        int Cty,
        long ExpiredTime2,
        int UnbindCount,
        int UnbindDeduct,
        int AttrUnbindLimitTime,
        int AttrUnbindDeductTime,
        int AttrUnbindFreeCount,
        int AttrUnbindMaxCount,
        int BindIp,
        int BanTime,
        string Owner,
        int BindUser,
        int NowBindMachineNum,
        int BindMachineNum,
        string PcSign2,
        int BanDurationTime,
        int GiveBackBanTime,
        int PicxCount,
        int LockBindPcsign,
        long LastRechargeTime,
        byte[]? UserExtraData);

    internal readonly record struct CardBindingRecord(string Card, string? PcSign);

    internal readonly record struct CardListQueryOptions(
        int Page,
        int PageSize,
        string Status,
        IReadOnlyList<string> Creators,
        IReadOnlyList<string> Keywords,
        int SearchType);

    internal readonly record struct CardListResult(
        IReadOnlyList<CardRecord> Cards,
        IReadOnlyList<CardBindingRecord> Bindings,
        long Total);

    internal readonly record struct CardInsertRecord(
        string PrefixName,
        string Whom,
        string CardType,
        int Fyi,
        string State,
        int Bind,
        int OpenNum,
        string Ip,
        string Remarks,
        long CreateData,
        long ActivateTime,
        long ExpiredTime,
        long LastLoginTime,
        int DelState,
        double Price,
        int Cty,
        long ExpiredTime2,
        int AttrUnbindLimitTime,
        int AttrUnbindDeductTime,
        int AttrUnbindFreeCount,
        int AttrUnbindMaxCount,
        int BindIp,
        int BindMachineNum,
        int LockBindPcsign);

    internal readonly record struct ActivatedCardRecord(string Card, long ActivateTime);

    internal readonly record struct ActivatedCardQueryOptions(
        string Status,
        long? StartTime,
        long? EndTime,
        IReadOnlyList<string> Creators,
        IReadOnlyList<string> CardTypes);

    internal readonly record struct ActivatedCardQueryResult(
        IReadOnlyList<ActivatedCardRecord> Records,
        long Total);

    internal readonly record struct CardTrendRecord(string? Whom, string Day, long Count);

    [StructLayout(LayoutKind.Sequential)]
    private struct UsageDistributionEntryNative
    {
        public IntPtr Whom;
        public IntPtr Payload;
        public long ResolvedTotal;
        public long UpdatedAt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UsageDistributionRecordNative
    {
        public IntPtr Whom;
        public IntPtr Payload;
        public long ResolvedTotal;
        public long UpdatedAt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MultiSoftwareRecordNative
    {
        public IntPtr SoftwareName;
        public int State;
        public IntPtr Idc;
        public int Version;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IpLocationRecordNative
    {
        public IntPtr Ip;
        public IntPtr Province;
        public IntPtr City;
        public IntPtr District;
        public long UpdatedAt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlacklistMachineRecordNative
    {
        public IntPtr Value;
        public int Type;
        public IntPtr Remarks;
        public long RowId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlacklistLogRecordNative
    {
        public long Id;
        public IntPtr Ip;
        public IntPtr Card;
        public IntPtr PcSign;
        public IntPtr ErrEvents;
        public long Timestamp;
        public long RowId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AgentRecordNative
    {
        public IntPtr User;
        public IntPtr Password;
        public double AccountBalance;
        public long AccountTime;
        public IntPtr Duration;
        public IntPtr Authority;
        public IntPtr CardTypeAuthName;
        public int CardsEnable;
        public IntPtr Remarks;
        public IntPtr FNode;
        public int Stat;
        public int DeletedAt;
        public long DurationRaw;
        public double Parities;
        public double TotalParities;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AgentStatisticsNative
    {
        public long TotalCards;
        public long ActiveCards;
        public long UsedCards;
        public long ExpiredCards;
        public long SubAgents;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardCreatorRecordNative
    {
        public IntPtr Whom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardIpRecordNative
    {
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardTypeRecordNative
    {
        public IntPtr Name;
        public IntPtr Prefix;
        public int Duration;
        public int Fyi;
        public double Price;
        public IntPtr Param;
        public int Bind;
        public int OpenNum;
        public IntPtr Remarks;
        public int CannotBeChanged;
        public int AttrUnbindLimitTime;
        public int AttrUnbindDeductTime;
        public int AttrUnbindFreeCount;
        public int AttrUnbindMaxCount;
        public int BindIp;
        public int BindMachineNum;
        public int LockBindPcsign;
        public long ActivateTime;
        public long ExpiredTime;
        public long LastLoginTime;
        public int DelState;
        public int Cty;
        public long ExpiredTime2;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardRecordNative
    {
        public IntPtr PrefixName;
        public IntPtr Whom;
        public IntPtr CardType;
        public int Fyi;
        public IntPtr State;
        public int Bind;
        public int OpenNum;
        public int LoginCount;
        public IntPtr Ip;
        public IntPtr Remarks;
        public long CreateData;
        public long ActivateTime;
        public long ExpiredTime;
        public long LastLoginTime;
        public int DelState;
        public double Price;
        public int Cty;
        public long ExpiredTime2;
        public int UnbindCount;
        public int UnbindDeduct;
        public int AttrUnbindLimitTime;
        public int AttrUnbindDeductTime;
        public int AttrUnbindFreeCount;
        public int AttrUnbindMaxCount;
        public int BindIp;
        public int BanTime;
        public IntPtr Owner;
        public int BindUser;
        public int NowBindMachineNum;
        public int BindMachineNum;
        public IntPtr PcSign2;
        public int BanDurationTime;
        public int GiveBackBanTime;
        public int PicxCount;
        public int LockBindPcsign;
        public long LastRechargeTime;
        public IntPtr UserExtraData;
        public int UserExtraDataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardBindingRecordNative
    {
        public IntPtr Card;
        public IntPtr PcSign;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardInsertRecordNative
    {
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string PrefixName;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string Whom;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string CardType;
        public int Fyi;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string State;
        public int Bind;
        public int OpenNum;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string Ip;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public string Remarks;
        public long CreateData;
        public long ActivateTime;
        public long ExpiredTime;
        public long LastLoginTime;
        public int DelState;
        public double Price;
        public int Cty;
        public long ExpiredTime2;
        public int AttrUnbindLimitTime;
        public int AttrUnbindDeductTime;
        public int AttrUnbindFreeCount;
        public int AttrUnbindMaxCount;
        public int BindIp;
        public int BindMachineNum;
        public int LockBindPcsign;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ActivatedCardRecordNative
    {
        public IntPtr Card;
        public long ActivateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CardTrendRecordNative
    {
        public IntPtr Whom;
        public IntPtr Day;
        public long Count;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_usage_distribution_replace(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string software,
        UsageDistributionEntryNative[] entries,
        int entryCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_usage_distribution_get(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string software,
        IntPtr keys,
        int keyCount,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_usage_distribution_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_multi_software_get_all(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_multi_software_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_ip_location_get(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IntPtr ips,
        int ipCount,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_ip_location_upsert(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IpLocationRecordNative[] records,
        int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_ip_location_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_blacklist_get_machines(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        int hasTypeFilter,
        int typeValue,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_blacklist_add_machine(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
        int type,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string remarks,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_blacklist_delete_machines(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IntPtr values,
        int valueCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_blacklist_free_machines(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_blacklist_get_logs(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        int limit,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_blacklist_free_logs(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_get_all(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_get_by_username(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        out IntPtr record,
        out int hasValue,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_agent_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_set_status(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IntPtr usernames,
        int usernameCount,
        int enable,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_update_remark(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string remark,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        double balance,
        long timeStock,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string authority,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cardTypes,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string remark,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fnode,
        double parities,
        double totalParities,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_soft_delete(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IntPtr usernames,
        int usernameCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_update_password(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string password,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_add_balance(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        double balance,
        long timeStock,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_set_card_types(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cardTypes,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_agent_get_statistics(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string username,
        out AgentStatisticsNative statistics,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_type_get_all(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_type_get_by_name(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        out IntPtr record,
        out int hasValue,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_type_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_type_free_record(IntPtr record);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_get_by_key(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cardKey,
        out IntPtr record,
        out int hasValue,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_record(IntPtr record);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_delete_bindings(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cardKey,
        out long affectedRows,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_update_state(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string cardKey,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string state,
        int resetBanTime,
        int resetGiveBackBanTime,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_get_creators(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_creators(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_get_ips(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        IntPtr creators,
        int creatorCount,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_ips(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_query_list(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        int page,
        int pageSize,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string status,
        int searchType,
        IntPtr creators,
        int creatorCount,
        IntPtr keywords,
        int keywordCount,
        out IntPtr records,
        out int recordCount,
        out IntPtr bindings,
        out int bindingCount,
        out long total,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_records(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_bindings(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_insert_many(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPArray)] CardInsertRecordNative[] records,
        int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_query_activated(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string status,
        long startTime,
        int hasStartTime,
        long endTime,
        int hasEndTime,
        IntPtr creators,
        int creatorCount,
        IntPtr cardTypes,
        int cardTypeCount,
        out IntPtr records,
        out int recordCount,
        out long total,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_activated(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int sp_card_query_activation_trend(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dbPath,
        long startTime,
        long endTime,
        IntPtr creators,
        int creatorCount,
        int groupByWhom,
        out IntPtr records,
        out int recordCount,
        out IntPtr errorMessage);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_card_free_trend(IntPtr records, int recordCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void sp_free_error(IntPtr message);

    public static void ReplaceUsageDistributionEntries(
        string databasePath,
        string software,
        IReadOnlyList<UsageDistributionRecord> entries)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(software);
        ArgumentNullException.ThrowIfNull(entries);

        var nativeEntries = new UsageDistributionEntryNative[entries.Count];
        var allocated = new List<IntPtr>(entries.Count * 2);

        try
        {
            EnsureNativeLibraryIsAvailable();

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                nativeEntries[i] = new UsageDistributionEntryNative
                {
                    Whom = AllocateUtf8(entry.Whom),
                    Payload = AllocateUtf8(entry.Payload),
                    ResolvedTotal = entry.ResolvedTotal,
                    UpdatedAt = entry.UpdatedAt
                };

                allocated.Add(nativeEntries[i].Whom);
                allocated.Add(nativeEntries[i].Payload);
            }

            ExecuteNative(
                () =>
                {
                    var rc = sp_usage_distribution_replace(databasePath, software, nativeEntries, nativeEntries.Length, out var errorPtr);
                    HandleReturnCode(rc, errorPtr);
                });
        }
        finally
        {
            foreach (var ptr in allocated)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
        }
    }

    public static IReadOnlyList<UsageDistributionRecord> GetUsageDistributionEntries(
        string databasePath,
        string software,
        IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(software);
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return Array.Empty<UsageDistributionRecord>();
        }

        using var nativeKeys = new NativeUtf8StringArray(keys.Select(key => key ?? string.Empty));

        return ExecuteNative(
            () =>
            {
                var rc = sp_usage_distribution_get(databasePath, software, nativeKeys.Pointer, nativeKeys.Length, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<UsageDistributionRecord>();
                    }

                    var size = Marshal.SizeOf<UsageDistributionRecordNative>();
                    var results = new UsageDistributionRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var current = Marshal.PtrToStructure<UsageDistributionRecordNative>(recordsPtr + i * size);
                        var whom = Marshal.PtrToStringUTF8(current.Whom) ?? string.Empty;
                        var payload = Marshal.PtrToStringUTF8(current.Payload) ?? string.Empty;
                        results[i] = new UsageDistributionRecord(whom, payload, current.ResolvedTotal, current.UpdatedAt);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_usage_distribution_free_records(recordsPtr, count);
                    }
                }
            });
    }

    public static IReadOnlyList<MultiSoftwareRecord> GetMultiSoftwareRecords(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_multi_software_get_all(databasePath, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<MultiSoftwareRecord>();
                    }

                    var size = Marshal.SizeOf<MultiSoftwareRecordNative>();
                    var results = new MultiSoftwareRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var current = Marshal.PtrToStructure<MultiSoftwareRecordNative>(recordsPtr + i * size);
                        var name = Marshal.PtrToStringUTF8(current.SoftwareName) ?? string.Empty;
                        var idc = Marshal.PtrToStringUTF8(current.Idc) ?? string.Empty;
                        results[i] = new MultiSoftwareRecord(name, current.State, idc, current.Version);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_multi_software_free_records(recordsPtr, count);
                    }
                }
            });
    }

    public static IReadOnlyList<IpLocationRecord> GetIpLocations(
        string databasePath,
        IReadOnlyList<string> ips)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(ips);

        if (ips.Count == 0)
        {
            return Array.Empty<IpLocationRecord>();
        }

        using var nativeIps = new NativeUtf8StringArray(ips.Select(ip => ip ?? string.Empty));

        return ExecuteNative(
            () =>
            {
                var rc = sp_ip_location_get(databasePath, nativeIps.Pointer, nativeIps.Length, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<IpLocationRecord>();
                    }

                    var size = Marshal.SizeOf<IpLocationRecordNative>();
                    var results = new IpLocationRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var current = Marshal.PtrToStructure<IpLocationRecordNative>(recordsPtr + i * size);
                        var ip = Marshal.PtrToStringUTF8(current.Ip) ?? string.Empty;
                        var province = Marshal.PtrToStringUTF8(current.Province) ?? string.Empty;
                        var city = Marshal.PtrToStringUTF8(current.City) ?? string.Empty;
                        var district = Marshal.PtrToStringUTF8(current.District) ?? string.Empty;
                        results[i] = new IpLocationRecord(ip, province, city, district, current.UpdatedAt);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_ip_location_free_records(recordsPtr, count);
                    }
                }
            });
    }

    public static void UpsertIpLocations(string databasePath, IReadOnlyList<IpLocationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return;
        }

        var nativeRecords = new IpLocationRecordNative[records.Count];
        var allocated = new List<IntPtr>(records.Count * 4);

        try
        {
            EnsureNativeLibraryIsAvailable();

            for (var i = 0; i < records.Count; i++)
            {
                var record = records[i];
                nativeRecords[i] = new IpLocationRecordNative
                {
                    Ip = AllocateUtf8(record.Ip),
                    Province = AllocateUtf8(record.Province),
                    City = AllocateUtf8(record.City),
                    District = AllocateUtf8(record.District),
                    UpdatedAt = record.UpdatedAt
                };

                allocated.Add(nativeRecords[i].Ip);
                allocated.Add(nativeRecords[i].Province);
                allocated.Add(nativeRecords[i].City);
                allocated.Add(nativeRecords[i].District);
            }

            ExecuteNative(
                () =>
                {
                    var rc = sp_ip_location_upsert(databasePath, nativeRecords, nativeRecords.Length, out var errorPtr);
                    HandleReturnCode(rc, errorPtr);
                });
        }
        finally
        {
            foreach (var ptr in allocated)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
        }
    }

    public static IReadOnlyList<BlacklistMachineRecord> GetBlacklistMachines(string databasePath, int? type)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_blacklist_get_machines(databasePath, type.HasValue ? 1 : 0, type ?? 0, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<BlacklistMachineRecord>();
                    }

                    var size = Marshal.SizeOf<BlacklistMachineRecordNative>();
                    var results = new BlacklistMachineRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var current = Marshal.PtrToStructure<BlacklistMachineRecordNative>(recordsPtr + i * size);
                        var value = Marshal.PtrToStringUTF8(current.Value) ?? string.Empty;
                        var remarks = Marshal.PtrToStringUTF8(current.Remarks) ?? string.Empty;
                        results[i] = new BlacklistMachineRecord(value, current.Type, remarks, current.RowId);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_blacklist_free_machines(recordsPtr, count);
                    }
                }
            });
    }

    public static void AddBlacklistMachine(string databasePath, string value, int type, string remarks)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(value);

        ExecuteNative(
            () =>
            {
                var rc = sp_blacklist_add_machine(databasePath, value, type, remarks ?? string.Empty, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void DeleteBlacklistMachines(string databasePath, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count == 0)
        {
            return;
        }

        using var nativeValues = new NativeUtf8StringArray(values.Select(value => value ?? string.Empty));

        ExecuteNative(
            () =>
            {
                var rc = sp_blacklist_delete_machines(databasePath, nativeValues.Pointer, nativeValues.Length, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static IReadOnlyList<BlacklistLogRecord> GetBlacklistLogs(string databasePath, int limit)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_blacklist_get_logs(databasePath, limit, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<BlacklistLogRecord>();
                    }

                    var size = Marshal.SizeOf<BlacklistLogRecordNative>();
                    var results = new BlacklistLogRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var current = Marshal.PtrToStructure<BlacklistLogRecordNative>(recordsPtr + i * size);
                        var ip = Marshal.PtrToStringUTF8(current.Ip) ?? string.Empty;
                        var card = Marshal.PtrToStringUTF8(current.Card) ?? string.Empty;
                        var pcSign = Marshal.PtrToStringUTF8(current.PcSign) ?? string.Empty;
                        var errEvents = Marshal.PtrToStringUTF8(current.ErrEvents) ?? string.Empty;
                        results[i] = new BlacklistLogRecord(current.Id, ip, card, pcSign, errEvents, current.Timestamp, current.RowId);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_blacklist_free_logs(recordsPtr, count);
                    }
                }
            });
    }

    public static IReadOnlyList<AgentRecord> GetAgents(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_agent_get_all(databasePath, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<AgentRecord>();
                    }

                    var size = Marshal.SizeOf<AgentRecordNative>();
                    var results = new AgentRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var native = Marshal.PtrToStructure<AgentRecordNative>(recordsPtr + i * size);
                        results[i] = ToAgentRecord(native);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_agent_free_records(recordsPtr, count);
                    }
                }
            });
    }

    public static AgentRecord? GetAgent(string databasePath, string username)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);

        return ExecuteNative(
            () =>
            {
                var rc = sp_agent_get_by_username(databasePath, username, out var recordPtr, out var hasValue, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (hasValue == 0 || recordPtr == IntPtr.Zero)
                    {
                        return (AgentRecord?)null;
                    }

                    var native = Marshal.PtrToStructure<AgentRecordNative>(recordPtr);
                    return (AgentRecord?)ToAgentRecord(native);
                }
                finally
                {
                    if (recordPtr != IntPtr.Zero)
                    {
                        sp_agent_free_records(recordPtr, hasValue != 0 ? 1 : 0);
                    }
                }
            });
    }

    public static void SetAgentStatuses(string databasePath, IEnumerable<string> usernames, bool enable)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(usernames);

        var normalized = usernames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        ExecuteNative(
            () =>
            {
                using var nativeUsernames = new NativeUtf8StringArray(normalized);
                var rc = sp_agent_set_status(
                    databasePath,
                    nativeUsernames.Pointer,
                    nativeUsernames.Length,
                    enable ? 1 : 0,
                    out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void UpdateAgentRemark(string databasePath, string username, string remark)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);
        remark ??= string.Empty;

        ExecuteNative(
            () =>
            {
                var rc = sp_agent_update_remark(databasePath, username, remark, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void CreateAgent(
        string databasePath,
        string username,
        string password,
        double balance,
        long timeStock,
        string authority,
        string cardTypes,
        string remark,
        string fnode,
        double parities,
        double totalParities)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        authority ??= string.Empty;
        cardTypes ??= string.Empty;
        remark ??= string.Empty;
        fnode ??= string.Empty;

        ExecuteNative(
            () =>
            {
                var rc = sp_agent_create(
                    databasePath,
                    username,
                    password,
                    balance,
                    timeStock,
                    authority,
                    cardTypes,
                    remark,
                    fnode,
                    parities,
                    totalParities,
                    out var errorPtr);

                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void SoftDeleteAgents(string databasePath, IEnumerable<string> usernames)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(usernames);

        var normalized = usernames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            return;
        }

        ExecuteNative(
            () =>
            {
                using var nativeUsernames = new NativeUtf8StringArray(normalized);
                var rc = sp_agent_soft_delete(databasePath, nativeUsernames.Pointer, nativeUsernames.Length, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void UpdateAgentPassword(string databasePath, string username, string password)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        ExecuteNative(
            () =>
            {
                var rc = sp_agent_update_password(databasePath, username, password, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void AddAgentBalance(string databasePath, string username, double balance, long timeStock)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);

        ExecuteNative(
            () =>
            {
                var rc = sp_agent_add_balance(databasePath, username, balance, timeStock, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static void SetAgentCardTypes(string databasePath, string username, string cardTypes)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);
        cardTypes ??= string.Empty;

        ExecuteNative(
            () =>
            {
                var rc = sp_agent_set_card_types(databasePath, username, cardTypes, out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    public static AgentStatisticsRecord GetAgentStatistics(string databasePath, string username)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(username);

        return ExecuteNative(
            () =>
            {
                var rc = sp_agent_get_statistics(databasePath, username, out var native, out var errorPtr);
                HandleReturnCode(rc, errorPtr);

                return new AgentStatisticsRecord(
                    native.TotalCards,
                    native.ActiveCards,
                    native.UsedCards,
                    native.ExpiredCards,
                    native.SubAgents);
            });
    }

    public static IReadOnlyList<string> GetCardCreators(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_card_get_creators(databasePath, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<string>();
                    }

                    var size = Marshal.SizeOf<CardCreatorRecordNative>();
                    var result = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        var currentPtr = IntPtr.Add(recordsPtr, i * size);
                        var native = Marshal.PtrToStructure<CardCreatorRecordNative>(currentPtr);
                        result[i] = Marshal.PtrToStringUTF8(native.Whom) ?? string.Empty;
                    }

                    return result;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_card_free_creators(recordsPtr, count);
                    }
                }
            });
    }

    public static IReadOnlyList<string> GetCardIpAddresses(string databasePath, IEnumerable<string>? creators)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var normalized = (creators ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                using var nativeCreators = new NativeUtf8StringArray(normalized);

                var rc = sp_card_get_ips(
                    databasePath,
                    nativeCreators.Pointer,
                    nativeCreators.Length,
                    out var recordsPtr,
                    out var count,
                    out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<string>();
                    }

                    var size = Marshal.SizeOf<CardIpRecordNative>();
                    var result = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        var currentPtr = IntPtr.Add(recordsPtr, i * size);
                        var native = Marshal.PtrToStructure<CardIpRecordNative>(currentPtr);
                        result[i] = Marshal.PtrToStringUTF8(native.Value) ?? string.Empty;
                    }

                    return result;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_card_free_ips(recordsPtr, count);
                    }
                }
            });
    }

    public static CardListResult QueryCardList(string databasePath, CardListQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        var safePage = options.Page <= 0 ? 1 : options.Page;
        var safePageSize = options.PageSize <= 0 ? 20 : options.PageSize;

        var creatorArray = (options.Creators ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var keywordArray = (options.Keywords ?? Array.Empty<string>())
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Where(keyword => keyword.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        string[] keywordParameters;
        switch (options.SearchType)
        {
            case 1:
            case 3:
                keywordParameters = keywordArray;
                break;
            default:
                keywordParameters = keywordArray.Select(keyword => $"%{keyword}%").ToArray();
                break;
        }

        var status = options.Status?.Trim() ?? string.Empty;

        var creatorsNative = creatorArray.Length > 0 ? creatorArray : Array.Empty<string>();
        var keywordsNative = keywordParameters.Length > 0 ? keywordParameters : Array.Empty<string>();

        using var nativeCreators = new NativeUtf8StringArray(creatorsNative);
        using var nativeKeywords = new NativeUtf8StringArray(keywordsNative);

        EnsureNativeLibraryIsAvailable();

        var rc = sp_card_query_list(
            databasePath,
            safePage,
            safePageSize,
            status,
            options.SearchType,
            nativeCreators.Pointer,
            nativeCreators.Length,
            nativeKeywords.Pointer,
            nativeKeywords.Length,
            out var recordsPtr,
            out var recordCount,
            out var bindingsPtr,
            out var bindingCount,
            out var total,
            out var errorPtr);

        try
        {
            HandleReturnCode(rc, errorPtr);

            var cards = Array.Empty<CardRecord>();
            if (recordsPtr != IntPtr.Zero && recordCount > 0)
            {
                var size = Marshal.SizeOf<CardRecordNative>();
                cards = new CardRecord[recordCount];
                for (var i = 0; i < recordCount; i++)
                {
                    var currentPtr = IntPtr.Add(recordsPtr, i * size);
                    var native = Marshal.PtrToStructure<CardRecordNative>(currentPtr);
                    cards[i] = ToCardRecord(native);
                }
            }

            var bindings = Array.Empty<CardBindingRecord>();
            if (bindingsPtr != IntPtr.Zero && bindingCount > 0)
            {
                var size = Marshal.SizeOf<CardBindingRecordNative>();
                bindings = new CardBindingRecord[bindingCount];
                for (var i = 0; i < bindingCount; i++)
                {
                    var currentPtr = IntPtr.Add(bindingsPtr, i * size);
                    var native = Marshal.PtrToStructure<CardBindingRecordNative>(currentPtr);
                    var card = Marshal.PtrToStringUTF8(native.Card) ?? string.Empty;
                    var pcSign = Marshal.PtrToStringUTF8(native.PcSign);
                    bindings[i] = new CardBindingRecord(card, pcSign);
                }
            }

            return new CardListResult(cards, bindings, total);
        }
        finally
        {
            if (recordsPtr != IntPtr.Zero)
            {
                sp_card_free_records(recordsPtr, recordCount);
            }

            if (bindingsPtr != IntPtr.Zero)
            {
                sp_card_free_bindings(bindingsPtr, bindingCount);
            }
        }
    }

    public static void InsertCards(string databasePath, IReadOnlyList<CardInsertRecord> records)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return;
        }

        var nativeRecords = new CardInsertRecordNative[records.Count];
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            nativeRecords[i] = new CardInsertRecordNative
            {
                PrefixName = record.PrefixName ?? string.Empty,
                Whom = record.Whom ?? string.Empty,
                CardType = record.CardType ?? string.Empty,
                Fyi = record.Fyi,
                State = record.State ?? string.Empty,
                Bind = record.Bind,
                OpenNum = record.OpenNum,
                Ip = record.Ip ?? string.Empty,
                Remarks = record.Remarks ?? string.Empty,
                CreateData = record.CreateData,
                ActivateTime = record.ActivateTime,
                ExpiredTime = record.ExpiredTime,
                LastLoginTime = record.LastLoginTime,
                DelState = record.DelState,
                Price = record.Price,
                Cty = record.Cty,
                ExpiredTime2 = record.ExpiredTime2,
                AttrUnbindLimitTime = record.AttrUnbindLimitTime,
                AttrUnbindDeductTime = record.AttrUnbindDeductTime,
                AttrUnbindFreeCount = record.AttrUnbindFreeCount,
                AttrUnbindMaxCount = record.AttrUnbindMaxCount,
                BindIp = record.BindIp,
                BindMachineNum = record.BindMachineNum,
                LockBindPcsign = record.LockBindPcsign
            };
        }

        EnsureNativeLibraryIsAvailable();

        var rc = sp_card_insert_many(databasePath, nativeRecords, nativeRecords.Length, out var errorPtr);
        HandleReturnCode(rc, errorPtr);
    }

    public static ActivatedCardQueryResult QueryActivatedCards(string databasePath, ActivatedCardQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        var creators = (options.Creators ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var cardTypes = (options.CardTypes ?? Array.Empty<string>())
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Select(type => type.Trim())
            .Where(type => type.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var creatorsNative = creators.Length > 0 ? creators : Array.Empty<string>();
        var cardTypesNative = cardTypes.Length > 0 ? cardTypes : Array.Empty<string>();

        using var nativeCreators = new NativeUtf8StringArray(creatorsNative);
        using var nativeCardTypes = new NativeUtf8StringArray(cardTypesNative);

        var status = options.Status?.Trim() ?? string.Empty;
        var hasStart = options.StartTime.HasValue ? 1 : 0;
        var hasEnd = options.EndTime.HasValue ? 1 : 0;
        var startValue = options.StartTime.GetValueOrDefault();
        var endValue = options.EndTime.GetValueOrDefault();

        EnsureNativeLibraryIsAvailable();

        var rc = sp_card_query_activated(
            databasePath,
            status,
            startValue,
            hasStart,
            endValue,
            hasEnd,
            nativeCreators.Pointer,
            nativeCreators.Length,
            nativeCardTypes.Pointer,
            nativeCardTypes.Length,
            out var recordsPtr,
            out var recordCount,
            out var total,
            out var errorPtr);

        try
        {
            HandleReturnCode(rc, errorPtr);

            if (recordsPtr == IntPtr.Zero || recordCount <= 0)
            {
                return new ActivatedCardQueryResult(Array.Empty<ActivatedCardRecord>(), total);
            }

            var size = Marshal.SizeOf<ActivatedCardRecordNative>();
            var result = new ActivatedCardRecord[recordCount];
            for (var i = 0; i < recordCount; i++)
            {
                var currentPtr = IntPtr.Add(recordsPtr, i * size);
                var native = Marshal.PtrToStructure<ActivatedCardRecordNative>(currentPtr);
                var card = Marshal.PtrToStringUTF8(native.Card) ?? string.Empty;
                result[i] = new ActivatedCardRecord(card, native.ActivateTime);
            }

            return new ActivatedCardQueryResult(result, total);
        }
        finally
        {
            if (recordsPtr != IntPtr.Zero)
            {
                sp_card_free_activated(recordsPtr, recordCount);
            }
        }
    }

    public static IReadOnlyList<CardTrendRecord> QueryActivationTrend(
        string databasePath,
        long startTime,
        long endTime,
        IEnumerable<string>? creators,
        bool groupByWhom)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        var creatorArray = (creators ?? Array.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var creatorsNative = creatorArray.Length > 0 ? creatorArray : Array.Empty<string>();

        using var nativeCreators = new NativeUtf8StringArray(creatorsNative);

        EnsureNativeLibraryIsAvailable();

        var rc = sp_card_query_activation_trend(
            databasePath,
            startTime,
            endTime,
            nativeCreators.Pointer,
            nativeCreators.Length,
            groupByWhom ? 1 : 0,
            out var recordsPtr,
            out var recordCount,
            out var errorPtr);

        try
        {
            HandleReturnCode(rc, errorPtr);

            if (recordsPtr == IntPtr.Zero || recordCount <= 0)
            {
                return Array.Empty<CardTrendRecord>();
            }

            var size = Marshal.SizeOf<CardTrendRecordNative>();
            var result = new CardTrendRecord[recordCount];
            for (var i = 0; i < recordCount; i++)
            {
                var currentPtr = IntPtr.Add(recordsPtr, i * size);
                var native = Marshal.PtrToStructure<CardTrendRecordNative>(currentPtr);
                var whom = Marshal.PtrToStringUTF8(native.Whom);
                var day = Marshal.PtrToStringUTF8(native.Day) ?? string.Empty;
                result[i] = new CardTrendRecord(whom, day, native.Count);
            }

            return result;
        }
        finally
        {
            if (recordsPtr != IntPtr.Zero)
            {
                sp_card_free_trend(recordsPtr, recordCount);
            }
        }
    }

    public static IReadOnlyList<CardTypeRecord> GetCardTypes(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);

        return ExecuteNative(
            () =>
            {
                var rc = sp_card_type_get_all(databasePath, out var recordsPtr, out var count, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (recordsPtr == IntPtr.Zero || count <= 0)
                    {
                        return Array.Empty<CardTypeRecord>();
                    }

                    var size = Marshal.SizeOf<CardTypeRecordNative>();
                    var results = new CardTypeRecord[count];
                    for (var i = 0; i < count; i++)
                    {
                        var currentPtr = IntPtr.Add(recordsPtr, i * size);
                        var native = Marshal.PtrToStructure<CardTypeRecordNative>(currentPtr);
                        results[i] = ToCardTypeRecord(native);
                    }

                    return results;
                }
                finally
                {
                    if (recordsPtr != IntPtr.Zero)
                    {
                        sp_card_type_free_records(recordsPtr, count);
                    }
                }
            });
    }

    public static CardTypeRecord? GetCardTypeByName(string databasePath, string name)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(name);

        return ExecuteNative(
            () =>
            {
                var rc = sp_card_type_get_by_name(databasePath, name, out var recordPtr, out var hasValue, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (hasValue == 0 || recordPtr == IntPtr.Zero)
                    {
                        return (CardTypeRecord?)null;
                    }

                    var native = Marshal.PtrToStructure<CardTypeRecordNative>(recordPtr);
                    return (CardTypeRecord?)ToCardTypeRecord(native);
                }
                finally
                {
                    if (recordPtr != IntPtr.Zero)
                    {
                        sp_card_type_free_record(recordPtr);
                    }
                }
            });
    }

    public static CardRecord? GetCardByKey(string databasePath, string cardKey)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(cardKey);

        return ExecuteNative(
            () =>
            {
                var rc = sp_card_get_by_key(databasePath, cardKey, out var recordPtr, out var hasValue, out var errorPtr);
                try
                {
                    HandleReturnCode(rc, errorPtr);

                    if (hasValue == 0 || recordPtr == IntPtr.Zero)
                    {
                        return (CardRecord?)null;
                    }

                    var native = Marshal.PtrToStructure<CardRecordNative>(recordPtr);
                    return (CardRecord?)ToCardRecord(native);
                }
                finally
                {
                    if (recordPtr != IntPtr.Zero)
                    {
                        sp_card_free_record(recordPtr);
                    }
                }
            });
    }

    public static int DeleteCardBindings(string databasePath, string cardKey)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(cardKey);

        return ExecuteNative(
            () =>
            {
                var rc = sp_card_delete_bindings(databasePath, cardKey, out var affected, out var errorPtr);
                HandleReturnCode(rc, errorPtr);

                return affected switch
                {
                    < int.MinValue => int.MinValue,
                    > int.MaxValue => int.MaxValue,
                    _ => (int)affected
                };
            });
    }

    public static void UpdateCardState(
        string databasePath,
        string cardKey,
        string state,
        bool resetBanTime,
        bool resetGiveBackBanTime)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        ArgumentNullException.ThrowIfNull(cardKey);
        ArgumentNullException.ThrowIfNull(state);

        ExecuteNative(
            () =>
            {
                var rc = sp_card_update_state(
                    databasePath,
                    cardKey,
                    state,
                    resetBanTime ? 1 : 0,
                    resetGiveBackBanTime ? 1 : 0,
                    out var errorPtr);
                HandleReturnCode(rc, errorPtr);
            });
    }

    private sealed class NativeUtf8StringArray : IDisposable
    {
        private readonly IntPtr[] _pointers;
        private readonly List<IntPtr> _allocated;
        private readonly bool _isPinned;
        private GCHandle _handle;

        public NativeUtf8StringArray(IEnumerable<string?> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            _allocated = new List<IntPtr>();
            var pointerList = new List<IntPtr>();

            foreach (var value in values)
            {
                var ptr = Marshal.StringToCoTaskMemUTF8(value ?? string.Empty);
                _allocated.Add(ptr);
                pointerList.Add(ptr);
            }

            _pointers = pointerList.ToArray();
            if (_pointers.Length > 0)
            {
                _handle = GCHandle.Alloc(_pointers, GCHandleType.Pinned);
                _isPinned = true;
            }
        }

        public IntPtr Pointer => _isPinned ? _handle.AddrOfPinnedObject() : IntPtr.Zero;

        public int Length => _pointers.Length;

        public void Dispose()
        {
            if (_isPinned && _handle.IsAllocated)
            {
                _handle.Free();
            }

            foreach (var ptr in _allocated)
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                }
            }
        }
    }

    private static void HandleReturnCode(int code, IntPtr errorPtr)
    {
        if (code == 0)
        {
            if (errorPtr != IntPtr.Zero)
            {
                sp_free_error(errorPtr);
            }

            return;
        }

        try
        {
            var message = errorPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(errorPtr) : null;
            throw new InvalidOperationException(message ?? $"Native SQLite operation failed with code {code}.");
        }
        finally
        {
            if (errorPtr != IntPtr.Zero)
            {
                sp_free_error(errorPtr);
            }
        }
    }

    private static IntPtr AllocateUtf8(string? value)
    {
        return string.IsNullOrEmpty(value) ? Marshal.StringToCoTaskMemUTF8(string.Empty) : Marshal.StringToCoTaskMemUTF8(value);
    }

    private static AgentRecord ToAgentRecord(AgentRecordNative native)
    {
        var user = Marshal.PtrToStringUTF8(native.User) ?? string.Empty;
        var password = Marshal.PtrToStringUTF8(native.Password) ?? string.Empty;
        var duration = Marshal.PtrToStringUTF8(native.Duration) ?? string.Empty;
        var authority = Marshal.PtrToStringUTF8(native.Authority) ?? string.Empty;
        var cardTypes = Marshal.PtrToStringUTF8(native.CardTypeAuthName) ?? string.Empty;
        var remarks = Marshal.PtrToStringUTF8(native.Remarks) ?? string.Empty;
        var fnode = Marshal.PtrToStringUTF8(native.FNode) ?? string.Empty;

        return new AgentRecord(
            user,
            password,
            native.AccountBalance,
            native.AccountTime,
            duration,
            authority,
            cardTypes,
            native.CardsEnable,
            remarks,
            fnode,
            native.Stat,
            native.DeletedAt,
            native.DurationRaw,
            native.Parities,
            native.TotalParities);
    }

    private static CardTypeRecord ToCardTypeRecord(CardTypeRecordNative native)
    {
        var name = Marshal.PtrToStringUTF8(native.Name) ?? string.Empty;
        var prefix = Marshal.PtrToStringUTF8(native.Prefix) ?? string.Empty;
        var param = Marshal.PtrToStringUTF8(native.Param) ?? string.Empty;
        var remarks = Marshal.PtrToStringUTF8(native.Remarks) ?? string.Empty;

        return new CardTypeRecord(
            name,
            prefix,
            native.Duration,
            native.Fyi,
            native.Price,
            param,
            native.Bind,
            native.OpenNum,
            remarks,
            native.CannotBeChanged,
            native.AttrUnbindLimitTime,
            native.AttrUnbindDeductTime,
            native.AttrUnbindFreeCount,
            native.AttrUnbindMaxCount,
            native.BindIp,
            native.BindMachineNum,
            native.LockBindPcsign,
            native.ActivateTime,
            native.ExpiredTime,
            native.LastLoginTime,
            native.DelState,
            native.Cty,
            native.ExpiredTime2);
    }

    private static CardRecord ToCardRecord(CardRecordNative native)
    {
        var prefix = Marshal.PtrToStringUTF8(native.PrefixName) ?? string.Empty;
        var whom = Marshal.PtrToStringUTF8(native.Whom) ?? string.Empty;
        var cardType = Marshal.PtrToStringUTF8(native.CardType) ?? string.Empty;
        var state = Marshal.PtrToStringUTF8(native.State) ?? string.Empty;
        var ip = Marshal.PtrToStringUTF8(native.Ip) ?? string.Empty;
        var remarks = Marshal.PtrToStringUTF8(native.Remarks) ?? string.Empty;
        var owner = Marshal.PtrToStringUTF8(native.Owner) ?? string.Empty;
        var pcSign2 = Marshal.PtrToStringUTF8(native.PcSign2) ?? string.Empty;

        byte[]? userExtra = null;
        if (native.UserExtraData != IntPtr.Zero && native.UserExtraDataLength > 0)
        {
            userExtra = new byte[native.UserExtraDataLength];
            Marshal.Copy(native.UserExtraData, userExtra, 0, native.UserExtraDataLength);
        }

        return new CardRecord(
            prefix,
            whom,
            cardType,
            native.Fyi,
            state,
            native.Bind,
            native.OpenNum,
            native.LoginCount,
            ip,
            remarks,
            native.CreateData,
            native.ActivateTime,
            native.ExpiredTime,
            native.LastLoginTime,
            native.DelState,
            native.Price,
            native.Cty,
            native.ExpiredTime2,
            native.UnbindCount,
            native.UnbindDeduct,
            native.AttrUnbindLimitTime,
            native.AttrUnbindDeductTime,
            native.AttrUnbindFreeCount,
            native.AttrUnbindMaxCount,
            native.BindIp,
            native.BanTime,
            owner,
            native.BindUser,
            native.NowBindMachineNum,
            native.BindMachineNum,
            pcSign2,
            native.BanDurationTime,
            native.GiveBackBanTime,
            native.PicxCount,
            native.LockBindPcsign,
            native.LastRechargeTime,
            userExtra);
    }
}


