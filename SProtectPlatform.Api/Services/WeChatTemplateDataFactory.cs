using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectPlatform.Api.Controllers;
using SProtectPlatform.Api.Models.Dto;

namespace SProtectPlatform.Api.Services;

public interface IWeChatTemplateDataFactory
{
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string templateKey,
        string userType,
        int userId,
        CancellationToken cancellationToken = default);
}

public sealed class WeChatTemplateDataFactory : IWeChatTemplateDataFactory
{
    private readonly IAgentService _agentService;
    private readonly IAuthorService _authorService;
    private readonly IBindingService _bindingService;
    private readonly ILogger<WeChatTemplateDataFactory> _logger;

    public WeChatTemplateDataFactory(
        IAgentService agentService,
        IAuthorService authorService,
        IBindingService bindingService,
        ILogger<WeChatTemplateDataFactory> logger)
    {
        _agentService = agentService;
        _authorService = authorService;
        _bindingService = bindingService;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string templateKey,
        string userType,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            switch (templateKey)
            {
                case WeChatTemplateKeys.InstantCommunication:
                    await PopulateInstantCommunicationAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                case WeChatTemplateKeys.BlacklistAlert:
                    await PopulateBlacklistAlertAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                case WeChatTemplateKeys.SettlementNotice:
                    await PopulateSettlementNoticeAsync(data, userType, userId, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成模板 {TemplateKey} 的动态数据时发生异常", templateKey);
        }

        return data;
    }

    private async Task PopulateInstantCommunicationAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? subjectName = null;
        string? systemName = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var agent = await _agentService.GetAuthRecordByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (agent != null)
            {
                subjectName = Prefer(agent.DisplayName, agent.Username, agent.Email);
            }

            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            if (binding != null)
            {
                systemName = Prefer(binding.AuthorDisplayName, binding.SoftwareCode);
            }
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (author != null)
            {
                subjectName = Prefer(author.DisplayName, author.Email, author.Username);
                systemName = Prefer(author.DisplayName, author.SoftwareCode);
            }
        }

        if (!string.IsNullOrWhiteSpace(subjectName))
        {
            data.TryAdd("thing1", $"{subjectName} 有新的即时沟通消息");
        }

        data.TryAdd("phrase2", "待处理");

        var now = FormatDateTime(DateTimeOffset.UtcNow);
        data.TryAdd("time4", now);
        data.TryAdd("time5", now);

        if (string.IsNullOrWhiteSpace(systemName))
        {
            systemName = subjectName;
        }

        if (!string.IsNullOrWhiteSpace(systemName))
        {
            data.TryAdd("thing6", systemName);
        }
    }

    private async Task PopulateBlacklistAlertAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? softwareName = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            softwareName = Prefer(binding?.AuthorDisplayName, binding?.SoftwareCode);
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            softwareName = Prefer(author?.DisplayName, author?.SoftwareCode);
        }

        var now = FormatDateTime(DateTimeOffset.UtcNow);
        data.TryAdd("time3", now);

        if (!string.IsNullOrWhiteSpace(softwareName))
        {
            data.TryAdd("thing5", $"软件 {softwareName} 检测到新的黑名单异常，请尽快核查处理");
        }
    }

    private async Task PopulateSettlementNoticeAsync(
        IDictionary<string, string> data,
        string userType,
        int userId,
        CancellationToken cancellationToken)
    {
        string? softwareCode = null;
        string? softwareName = null;
        string? agentLabel = null;

        if (string.Equals(userType, Roles.Agent, StringComparison.OrdinalIgnoreCase))
        {
            var agent = await _agentService.GetAuthRecordByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            agentLabel = Prefer(agent?.DisplayName, agent?.Username, agent?.Email);

            var binding = await GetPrimaryBindingForAgentAsync(userId, cancellationToken).ConfigureAwait(false);
            if (binding != null)
            {
                softwareCode = binding.SoftwareCode;
                softwareName = Prefer(binding.AuthorDisplayName, binding.SoftwareType);
            }
        }
        else if (string.Equals(userType, Roles.Author, StringComparison.OrdinalIgnoreCase))
        {
            var author = await _authorService.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (author != null)
            {
                softwareCode = author.SoftwareCode;
                softwareName = Prefer(author.DisplayName, author.SoftwareType);
            }
        }

        if (!string.IsNullOrWhiteSpace(softwareCode))
        {
            data.TryAdd("character_string1", softwareCode);
        }

        data.TryAdd("time3", FormatDate(DateTimeOffset.UtcNow));

        var summary = !string.IsNullOrWhiteSpace(softwareName)
            ? $"{softwareName} 待结算，请及时处理"
            : null;

        if (string.IsNullOrWhiteSpace(summary) && !string.IsNullOrWhiteSpace(agentLabel))
        {
            summary = $"账户 {agentLabel} 有新的结算提醒";
        }

        if (!string.IsNullOrWhiteSpace(summary))
        {
            data.TryAdd("thing5", summary);
        }

        if (!data.ContainsKey("number6"))
        {
            data.TryAdd("number6", "1");
        }
    }

    private async Task<BindingRecord?> GetPrimaryBindingForAgentAsync(int agentId, CancellationToken cancellationToken)
    {
        var bindings = await _bindingService.GetBindingsForAgentAsync(agentId, cancellationToken).ConfigureAwait(false);
        return bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.SoftwareCode))
            .OrderByDescending(binding => binding.BindingId)
            .FirstOrDefault();
    }

    private static string? Prefer(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatDateTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
