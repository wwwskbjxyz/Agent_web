using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace SProtectAgentWeb.Api.Utilities;

public class PermissionHelper
{
    private static readonly Regex BracketRegex = new("\\[([^\\]]+)\\]", RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<ulong, string> PermissionNames = new Dictionary<ulong, string>
    {
        { 0x0000_0001, "启用/禁用卡密" },
        { 0x0000_0002, "删除未激活卡密" },
        { 0x0000_0004, "添加/启用/禁用子代理" },
        { 0x0000_0008, "启用卡密(归还封禁时间)" },
        { 0x0000_0010, "卡密充值(基于卡密类型)" },
        { 0x0000_0020, "查看所有下级代理及其卡密" },
        { 0x0000_0040, "解绑卡密" },
        { 0x0000_0080, "允许被其他代理查询卡密" },
        { 0x0000_0100, "生成卡密" },
    };

    public ulong ParseAuthority(string? authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            return 0;
        }

        var value = authority.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? authority[2..]
            : authority;

        return ulong.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var result)
            ? result
            : 0;
    }

    public bool HasPermission(string? authority, ulong requiredPerm)
    {
        var parsed = ParseAuthority(authority);
        return (parsed & requiredPerm) == requiredPerm;
    }

    public IReadOnlyList<string> GetPermissionStrings(string? authority)
    {
        var parsed = ParseAuthority(authority);
        return PermissionNames
            .Where(pair => (parsed & pair.Key) != 0)
            .Select(pair => pair.Value)
            .ToList();
    }

    public IReadOnlyList<string> ParseBracketList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return BracketRegex
            .Matches(value)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public string BuildBracketList(IEnumerable<string> items)
    {
        var sanitized = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => $"[{item.Trim()}]");
        return string.Join(",", sanitized);
    }

    public IReadOnlyList<string> ParseAgentFNode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return BracketRegex
            .Matches(value)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public string GetAgentParent(string? fnode)
    {
        var chain = ParseAgentFNode(fnode);
        return chain.Count < 2 ? string.Empty : chain[chain.Count - 2];
    }

    public IReadOnlyList<string> GetAgentChain(string? fnode)
    {
        var chain = ParseAgentFNode(fnode);
        if (chain.Count <= 1)
        {
            return Array.Empty<string>();
        }

        return chain.Take(chain.Count - 1).ToList();
    }

    public bool IsChildAgent(string? childFNode, string parentUsername)
    {
        if (string.IsNullOrWhiteSpace(parentUsername))
        {
            return false;
        }

        return ParseAgentFNode(childFNode).Contains(parentUsername);
    }

    public bool IsDirectChildAgent(string? childFNode, string parentUsername)
    {
        return string.Equals(GetAgentParent(childFNode), parentUsername, StringComparison.OrdinalIgnoreCase);
    }

    public string GenerateChildFNode(string? parentFNode, string childUsername)
    {
        var chain = ParseAgentFNode(parentFNode).ToList();
        chain.Add(childUsername);
        return BuildBracketList(chain);
    }
}