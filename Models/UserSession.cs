using System.Text.Json.Serialization;

namespace SProtectAgentWeb.Api.Models;

public class UserSession
{
    public string Username { get; set; } = string.Empty;

    [JsonIgnore]
    public string Password { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public IList<string> SoftwareList { get; set; } = new List<string>();

    public IDictionary<string, Agent> SoftwareAgentInfo { get; set; } = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase);

    public bool IsSuper { get; set; }
}
