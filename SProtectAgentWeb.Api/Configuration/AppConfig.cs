using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace SProtectAgentWeb.Api.Configuration;

public class AppConfig
{
    public const string ApplicationName = "SProtectAgentWeb";
    public const string Version = "1.0.0";

    [ConfigurationKeyName("服务器设置")]
    public ServerConfig Server { get; set; } = new();

    [ConfigurationKeyName("认证设置")]
    public JwtConfig Jwt { get; set; } = new();

    [ConfigurationKeyName("数据库")]
    public LanzouDatabaseConfig Lanzou { get; set; } = new();

    [ConfigurationKeyName("Heartbeat")]
    public HeartbeatConfig Heartbeat { get; set; } = new();

    [ConfigurationKeyName("PlatformIntegration")]
    public PlatformIntegrationConfig PlatformIntegration { get; set; } = new();

    [ConfigurationKeyName("聊天设置")]
    public ChatConfig Chat { get; set; } = new();

    public string GetServerAddress() => $"{Server.Host}:{Server.Port}";

    public string GetDataPath()
    {
        if (string.IsNullOrWhiteSpace(Server.DataPath))
        {
            return string.Empty;
        }

        var normalized = Server.DataPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim();

        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        var rootedPath = Path.IsPathRooted(normalized)
            ? normalized
            : Path.Combine(AppContext.BaseDirectory, normalized);

        var fullPath = Path.GetFullPath(rootedPath);
        if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
        {
            fullPath += Path.DirectorySeparatorChar;
        }

        return fullPath;
    }
}

public class ServerConfig
{
    [ConfigurationKeyName("服务器地址")]
    [Required]
    public string Host { get; set; } = "0.0.0.0";

    [ConfigurationKeyName("Host")]
    public string HostAlias
    {
        get => Host;
        set => Host = string.IsNullOrWhiteSpace(value) ? Host : value;
    }

    [ConfigurationKeyName("端口")]
    [Range(1, 65535)]
    public int Port { get; set; } = 5000;

    [ConfigurationKeyName("Port")]
    public int PortAlias
    {
        get => Port;
        set => Port = value is > 0 and <= 65535 ? value : Port;
    }

    [ConfigurationKeyName("数据库路径")]
    [Required]
    public string DataPath { get; set; } = string.Empty;

    [ConfigurationKeyName("DatabasePath")]
    public string DatabasePath
    {
        get => DataPath;
        set => DataPath = value ?? string.Empty;
    }

    [ConfigurationKeyName("SoftwareType")]
    public string SoftwareType { get; set; } = "SP";

    [ConfigurationKeyName("软件类型")]
    public string SoftwareTypeLegacy
    {
        get => SoftwareType;
        set => SoftwareType = string.IsNullOrWhiteSpace(value) ? SoftwareType : value.Trim();
    }

    [ConfigurationKeyName("权限")]
    public string? SuperUsers { get; set; } = "admin";

    public IReadOnlyCollection<string> GetSuperUsers()
    {
        var raw = SuperUsers;

        IEnumerable<string> users = string.IsNullOrWhiteSpace(raw)
            ? new[] { "admin" }
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new HashSet<string>(users, StringComparer.OrdinalIgnoreCase);
    }
}

public class JwtConfig
{
    [ConfigurationKeyName("JWT密钥")]
    [Required]
    public string Secret { get; set; } = string.Empty;

    [ConfigurationKeyName("JWT签发者")]
    public string Issuer { get; set; } = "SProtectAgentWeb";

    [ConfigurationKeyName("JWT受众")]
    public string Audience { get; set; } = "SProtectAgentWebClients";

    [ConfigurationKeyName("JWT访问令牌过期分钟数")]
    [Range(5, 1440)]
    public int AccessTokenExpirationMinutes { get; set; } = 120;

    [ConfigurationKeyName("JWT容许时间偏差秒数")]
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;
}

public class LanzouDatabaseConfig
{
    [ConfigurationKeyName("主机")]
    public string Host { get; set; } = "127.0.0.1";

    [ConfigurationKeyName("端口")]
    public int Port { get; set; } = 3306;

    [ConfigurationKeyName("数据库")]
    public string Database { get; set; } = "lanzou";

    [ConfigurationKeyName("用户名")]
    public string Username { get; set; } = "lanzou";

    [ConfigurationKeyName("密码")]
    public string Password { get; set; } = "";

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Database) || string.IsNullOrWhiteSpace(Username))
        {
            return string.Empty;
        }

        var host = string.IsNullOrWhiteSpace(Host) ? "127.0.0.1" : Host.Trim();
        var port = Port <= 0 ? 3306 : Port;
        var password = Password ?? string.Empty;

        return $"Server={host};Port={port};Database={Database.Trim()};Uid={Username.Trim()};Pwd={password};Character Set=utf8mb4;Allow User Variables=true;";
    }
}

public class HeartbeatConfig
{
    /// <summary>Shared secret required for heartbeat submissions. Leave empty to disable validation.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Seconds after which a heartbeat is considered stale.</summary>
    [Range(30, 3600)]
    public int ExpirationSeconds { get; set; } = 300;
}

public class PlatformIntegrationConfig
{
    [ConfigurationKeyName("SharedSecret")]
    public string SharedSecret { get; set; } = string.Empty;

    [ConfigurationKeyName("共享密钥")]
    public string SharedSecretLegacy
    {
        get => SharedSecret;
        set => SharedSecret = value ?? string.Empty;
    }

    [ConfigurationKeyName("AllowedClockSkewSeconds")]
    [Range(0, 900)]
    public int AllowedClockSkewSeconds { get; set; } = 300;

    [ConfigurationKeyName("允许时间偏差秒数")]
    public int AllowedClockSkewSecondsLegacy
    {
        get => AllowedClockSkewSeconds;
        set => AllowedClockSkewSeconds = value < 0 ? AllowedClockSkewSeconds : value;
    }
}

public class ChatConfig
{
    /// <summary>Retention window for chat messages, in hours.</summary>
    [ConfigurationKeyName("保留小时数")]
    [Range(1, 720)]
    public int RetentionHours { get; set; } = 24;

    [ConfigurationKeyName("允许图片消息")]
    public bool EnableImageMessages { get; set; } = false;

    [ConfigurationKeyName("允许表情包")]
    public bool EnableEmojiPicker { get; set; } = true;

    [ConfigurationKeyName("最大图片大小KB")]
    [Range(32, 10240)]
    public int MaxImageSizeKb { get; set; } = 2048;

}
