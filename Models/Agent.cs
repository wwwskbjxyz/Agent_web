using System.Text.Json.Serialization;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Models;

public class Agent
{
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public double AccountBalance { get; set; }
    public long AccountTime { get; set; }
    public string? Duration { get; set; }
    public string? Authority { get; set; }
    public string? CardTypeAuthName { get; set; }
    [JsonIgnore]
    public IList<string> CardTypeAuthNameArray { get; set; } = new List<string>();

    public bool CardsEnable { get; set; }
    public string? Remarks { get; set; }
    public string? FNode { get; set; }
    public int Stat { get; set; }
    public int Deltm { get; set; }
    public long Duration_ { get; set; }
    public double Parities { get; set; } = 100.0;
    public double TatalParities { get; set; } = 100.0;

    public bool IsExpired()
    {
        if (Duration_ <= 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Duration_ < now;
    }

    public ulong GetAuthorityUint64(PermissionHelper helper) => helper.ParseAuthority(Authority);

    public bool HasPermission(PermissionHelper helper, ulong required) => helper.HasPermission(Authority, required);

    public string GetParentAgent(PermissionHelper helper) => helper.GetAgentParent(FNode);

    public bool IsChildOf(PermissionHelper helper, string parent) => helper.IsChildAgent(FNode, parent);

    public bool IsDirectChildOf(PermissionHelper helper, string parent) => helper.IsDirectChildAgent(FNode, parent);
}
