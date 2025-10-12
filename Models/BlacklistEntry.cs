namespace SProtectAgentWeb.Api.Models;

public class BlacklistEntry
{
    public string Value { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? Remarks { get; set; }
    public string Software { get; set; } = string.Empty;
}

public class BlacklistLogEntry
{
    public long ID { get; set; }
    public string IP { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
    public string PCSign { get; set; } = string.Empty;
    public long Timestamp { get; set; }
    public string ErrEvents { get; set; } = string.Empty;
    public string Software { get; set; } = string.Empty;
}

