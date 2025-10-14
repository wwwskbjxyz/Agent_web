namespace SProtectAgentWeb.Api.Models;

public class MultiSoftware
{
    public string SoftwareName { get; set; } = string.Empty;
    public int State { get; set; }
    public string? Idc { get; set; }
    public int Version { get; set; }
    public string StatusText => State == 1 ? "启用" : "禁用";
}

public class SoftwareAgentInfo
{
    public string SoftwareName { get; set; } = string.Empty;
    public string? Idc { get; set; }
    public int State { get; set; }
    public SoftwareAgent? AgentInfo { get; set; }
    public IDictionary<string, bool> Permissions { get; set; } = new Dictionary<string, bool>();
}

public class SoftwareAgent
{
    public string Username { get; set; } = string.Empty;
    public double Balance { get; set; }
    public long TimeStock { get; set; }
    public IList<string> CardTypes { get; set; } = new List<string>();
    public string Status { get; set; } = "active";
    public string? Expiration { get; set; }
    public IDictionary<string, bool> Permissions { get; set; } = new Dictionary<string, bool>();
}
