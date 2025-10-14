using System;
using System.Collections.Generic;

namespace SProtectPlatform.Api.Options;

public sealed class WeChatOptions
{
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
    public WeChatTemplateOptions Templates { get; set; } = new();
    public WeChatTemplatePreviewOptions Previews { get; set; } = new();
    public int TokenSafetyMarginSeconds { get; set; } = 120;

    internal TimeSpan GetSafetyMargin() => TimeSpan.FromSeconds(Math.Clamp(TokenSafetyMarginSeconds, 30, 600));
}

public sealed class WeChatTemplateOptions
{
    public string? InstantCommunication { get; set; }
    public string? BlacklistAlert { get; set; }
    public string? SettlementNotice { get; set; }
}

public sealed class WeChatTemplatePreviewOptions
{
    public IDictionary<string, string>? InstantCommunication { get; set; }
    public IDictionary<string, string>? BlacklistAlert { get; set; }
    public IDictionary<string, string>? SettlementNotice { get; set; }

    public IReadOnlyDictionary<string, string>? GetForTemplate(string templateKey)
        => templateKey switch
        {
            Models.Dto.WeChatTemplateKeys.InstantCommunication => Normalize(InstantCommunication),
            Models.Dto.WeChatTemplateKeys.BlacklistAlert => Normalize(BlacklistAlert),
            Models.Dto.WeChatTemplateKeys.SettlementNotice => Normalize(SettlementNotice),
            _ => null
        };

    private static IReadOnlyDictionary<string, string>? Normalize(IDictionary<string, string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var value = pair.Value?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            result[pair.Key.Trim()] = value;
        }

        return result.Count == 0 ? null : result;
    }
}
