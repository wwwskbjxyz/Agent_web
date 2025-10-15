using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Data;
using SProtectPlatform.Api.Models.Dto;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Services;

public interface IWeChatMessageService
{
    Task<WeChatNotificationResultDto> SendToUserAsync(string templateKey, string userType, int userId, IReadOnlyDictionary<string, string> data, string? page, CancellationToken cancellationToken);
}

public sealed class WeChatMessageService : IWeChatMessageService
{
    private readonly IWeChatBindingService _bindingService;
    private readonly IWeChatAccessTokenProvider _tokenProvider;
    private readonly IWeChatTemplateDataFactory _templateDataFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMySqlConnectionFactory _connectionFactory;
    private readonly ILogger<WeChatMessageService> _logger;
    private readonly WeChatOptions _options;

    public WeChatMessageService(
        IWeChatBindingService bindingService,
        IWeChatAccessTokenProvider tokenProvider,
        IWeChatTemplateDataFactory templateDataFactory,
        IHttpClientFactory httpClientFactory,
        IMySqlConnectionFactory connectionFactory,
        IOptions<WeChatOptions> options,
        ILogger<WeChatMessageService> logger)
    {
        _bindingService = bindingService;
        _tokenProvider = tokenProvider;
        _templateDataFactory = templateDataFactory;
        _httpClientFactory = httpClientFactory;
        _connectionFactory = connectionFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<WeChatNotificationResultDto> SendToUserAsync(string templateKey, string userType, int userId, IReadOnlyDictionary<string, string> data, string? page, CancellationToken cancellationToken)
    {
        if (!WeChatTemplateKeys.IsKnown(templateKey))
        {
            return new WeChatNotificationResultDto(false, -1, "未知的模板类型");
        }

        var templateId = GetTemplateId(templateKey)?.Trim();
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return new WeChatNotificationResultDto(false, -2, "未配置模板 ID");
        }

        if (templateId.Contains("...", StringComparison.Ordinal))
        {
            return new WeChatNotificationResultDto(false, -2, "模板 ID 配置无效，请填写完整的模板编号");
        }

        var binding = await _bindingService.GetBindingAsync(userType, userId, cancellationToken);
        if (binding is null)
        {
            return new WeChatNotificationResultDto(false, -3, "用户未绑定微信");
        }

        IReadOnlyDictionary<string, string>? dynamicDefaults = null;
        try
        {
            dynamicDefaults = await _templateDataFactory.ResolveAsync(templateKey, userType, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成微信模板 {Template} 的动态数据时出现异常", templateKey);
        }

        IReadOnlyDictionary<string, string> requestData = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var preparedPayload = PreparePayloadData(templateKey, requestData, dynamicDefaults);
        var payloadData = NormalizeTemplatePayload(templateKey, preparedPayload.Data);

        if (ShouldSuppressNotification(templateKey, requestData, dynamicDefaults, payloadData, preparedPayload.IsPreviewPayload, out var suppression))
        {
            _logger.LogInformation(
                "微信模板 {Template} 推送被跳过：{Reason}",
                templateKey,
                suppression?.ErrorMessage ?? "业务规则不满足");

            return suppression ?? new WeChatNotificationResultDto(false, -10, "已跳过推送");
        }
        if (payloadData.Count == 0)
        {
            return new WeChatNotificationResultDto(false, -4, "模板缺少有效数据，请检查配置");
        }

        if (!ValidateTemplateData(templateKey, payloadData, preparedPayload.IsPreviewPayload, out var validationFailure))
        {
            return validationFailure ?? new WeChatNotificationResultDto(false, -4, "模板缺少有效数据，请检查配置");
        }

        var client = _httpClientFactory.CreateClient(nameof(IWeChatMessageService));
        var payload = BuildPayload(binding.OpenId, templateId, payloadData, page);

        WeChatNotificationResultDto result;
        var attempt = 0;
        do
        {
            var token = attempt == 0
                ? await _tokenProvider.GetAccessTokenAsync(cancellationToken)
                : await _tokenProvider.RefreshAccessTokenAsync(cancellationToken);

            var attemptResult = await SendOnceAsync(client, payload, token, cancellationToken);
            result = attemptResult.Result;

            if (attemptResult.ShouldRefreshToken && attempt == 0)
            {
                _logger.LogWarning(
                    "微信订阅消息发送失败（Status={StatusCode}, ErrCode={ErrorCode}），准备刷新 access_token 后重试。",
                    (int)attemptResult.StatusCode,
                    attemptResult.Result.ErrorCode);
                attempt++;
                continue;
            }

            break;
        } while (attempt < 2);

        await LogAsync(templateKey, templateId, userType, userId, binding.OpenId, payload, result, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("微信订阅消息发送失败：{Code} {Message}", result.ErrorCode, result.ErrorMessage);
        }

        return result;
    }

    private static bool LooksLikePreviewPayload(
        IReadOnlyDictionary<string, string>? defaults,
        IReadOnlyDictionary<string, string>? requestData)
    {
        if (defaults is null || defaults.Count == 0 || requestData is null || requestData.Count == 0)
        {
            return false;
        }

        var matchedKeys = 0;
        foreach (var pair in requestData)
        {
            if (!defaults.TryGetValue(pair.Key, out var defaultValue))
            {
                return false;
            }

            if (!string.Equals(defaultValue?.Trim(), pair.Value?.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            matchedKeys++;
        }

        if (matchedKeys == 0)
        {
            return false;
        }

        foreach (var pair in defaults)
        {
            if (!requestData.ContainsKey(pair.Key))
            {
                return false;
            }
        }

        return true;
    }

    private Dictionary<string, string> NormalizeTemplatePayload(
        string templateKey,
        IReadOnlyDictionary<string, string> payloadData)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in payloadData)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var trimmedKey = pair.Key.Trim();
            var trimmedValue = pair.Value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedValue))
            {
                continue;
            }

            normalized[trimmedKey] = NormalizeField(templateKey, trimmedKey, trimmedValue);
        }

        if (string.Equals(templateKey, WeChatTemplateKeys.InstantCommunication, StringComparison.OrdinalIgnoreCase))
        {
            normalized["time4"] = NormalizeDateTimeField(normalized.TryGetValue("time4", out var sendTime) ? sendTime : null)
                ?? FormatDateTime(DateTimeOffset.UtcNow);

            normalized["time5"] = NormalizeDateTimeField(normalized.TryGetValue("time5", out var pushTime) ? pushTime : null)
                ?? FormatDateTime(DateTimeOffset.UtcNow);
        }
        else if (string.Equals(templateKey, WeChatTemplateKeys.BlacklistAlert, StringComparison.OrdinalIgnoreCase))
        {
            normalized["time3"] = NormalizeDateTimeField(normalized.TryGetValue("time3", out var recordTime) ? recordTime : null)
                ?? FormatDateTime(DateTimeOffset.UtcNow);
        }
        else if (string.Equals(templateKey, WeChatTemplateKeys.SettlementNotice, StringComparison.OrdinalIgnoreCase))
        {
            normalized["time3"] = NormalizeDateField(normalized.TryGetValue("time3", out var cycle) ? cycle : null)
                ?? FormatDate(DateTimeOffset.UtcNow);

            if (!normalized.TryGetValue("thing5", out var summary) || string.IsNullOrWhiteSpace(summary))
            {
                var account = normalized.TryGetValue("character_string1", out var accountValue) ? accountValue : null;
                var cycleValue = normalized.TryGetValue("time3", out var cycleText) ? cycleText : null;
                var autoSummary = BuildSettlementSummary(account, cycleValue);
                if (!string.IsNullOrWhiteSpace(autoSummary))
                {
                    normalized["thing5"] = ClampToLength(autoSummary, 20);
                }
            }
            else
            {
                normalized["thing5"] = ClampToLength(summary, 20);
            }
        }

        return normalized;
    }

    private static string NormalizeField(string templateKey, string key, string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        if (key.StartsWith("time", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(templateKey, WeChatTemplateKeys.SettlementNotice, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(key, "time3", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return NormalizeDateTimeField(trimmed) ?? trimmed;
        }

        if (key.StartsWith("thing", StringComparison.OrdinalIgnoreCase))
        {
            return ClampToLength(trimmed, 20);
        }

        if (key.StartsWith("phrase", StringComparison.OrdinalIgnoreCase))
        {
            return ClampToLength(trimmed, 15);
        }

        if (key.StartsWith("character_string", StringComparison.OrdinalIgnoreCase))
        {
            return ClampToLength(trimmed, 32);
        }

        if (key.StartsWith("number", StringComparison.OrdinalIgnoreCase))
        {
            var digits = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                if (char.IsDigit(ch))
                {
                    digits.Append(ch);
                }
            }

            return digits.Length > 0 ? digits.ToString() : trimmed;
        }

        return trimmed;
    }

    private bool ValidateTemplateData(
        string templateKey,
        IReadOnlyDictionary<string, string> payloadData,
        bool isPreviewPayload,
        out WeChatNotificationResultDto? failure)
    {
        failure = null;

        string[] requiredFields = Array.Empty<string>();
        string[] overrideFields = Array.Empty<string>();

        if (string.Equals(templateKey, WeChatTemplateKeys.InstantCommunication, StringComparison.OrdinalIgnoreCase))
        {
            requiredFields = new[] { "thing1", "time4", "time5" };
            overrideFields = new[] { "time4", "time5" };
        }
        else if (string.Equals(templateKey, WeChatTemplateKeys.BlacklistAlert, StringComparison.OrdinalIgnoreCase))
        {
            requiredFields = new[] { "time3", "thing5" };
            overrideFields = new[] { "time3", "thing5" };
        }
        else if (string.Equals(templateKey, WeChatTemplateKeys.SettlementNotice, StringComparison.OrdinalIgnoreCase))
        {
            requiredFields = new[] { "character_string1", "amount2", "time3" };
            overrideFields = new[] { "character_string1", "amount2", "time3" };
        }

        foreach (var field in requiredFields)
        {
            if (!payloadData.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            {
                failure = new WeChatNotificationResultDto(false, -12, $"模板字段 {field} 缺少有效值");
                return false;
            }
        }

        if (!isPreviewPayload)
        {
            var defaults = _options.Previews.GetForTemplate(templateKey);
            if (defaults is not null && defaults.Count > 0)
            {
                foreach (var field in overrideFields)
                {
                    if (!payloadData.TryGetValue(field, out var actual) || string.IsNullOrWhiteSpace(actual))
                    {
                        continue;
                    }

                    if (defaults.TryGetValue(field, out var defaultValue) &&
                        string.Equals(defaultValue?.Trim(), actual.Trim(), StringComparison.Ordinal))
                    {
                        failure = new WeChatNotificationResultDto(false, -13, $"模板字段 {field} 仍为默认展示值，请提供真实数据");
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static string? NormalizeDateTimeField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ||
            DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed) ||
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return FormatDateTime(parsed);
        }

        return value.Trim();
    }

    private static string? NormalizeDateField(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ||
            DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed) ||
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return FormatDate(parsed);
        }

        return value.Trim();
    }

    private static string ClampToLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var enumerator = StringInfo.GetTextElementEnumerator(value);
        var builder = new StringBuilder();
        var count = 0;
        while (enumerator.MoveNext() && count < maxLength)
        {
            builder.Append(enumerator.GetTextElement());
            count++;
        }

        return builder.ToString();
    }

    private static string BuildSettlementSummary(string? account, string? cycle)
    {
        var trimmedAccount = string.IsNullOrWhiteSpace(account) ? null : account.Trim();
        var trimmedCycle = string.IsNullOrWhiteSpace(cycle) ? null : cycle.Trim();

        if (!string.IsNullOrEmpty(trimmedAccount) && !string.IsNullOrEmpty(trimmedCycle))
        {
            return $"{trimmedAccount} {trimmedCycle} 待结算";
        }

        if (!string.IsNullOrEmpty(trimmedAccount))
        {
            return $"{trimmedAccount} 待结算";
        }

        if (!string.IsNullOrEmpty(trimmedCycle))
        {
            return $"{trimmedCycle} 待结算";
        }

        return string.Empty;
    }

    private static string FormatDateTime(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private readonly struct PreparedTemplatePayload
    {
        public PreparedTemplatePayload(IReadOnlyDictionary<string, string> data, bool isPreviewPayload)
        {
            Data = data;
            IsPreviewPayload = isPreviewPayload;
        }

        public IReadOnlyDictionary<string, string> Data { get; }

        public bool IsPreviewPayload { get; }
    }

    private static IDictionary<string, object> BuildPayload(
        string openId,
        string templateId,
        IReadOnlyDictionary<string, string> data,
        string? page)
    {
        var formattedData = data is { Count: > 0 }
            ? data.ToDictionary(
                static kvp => kvp.Key,
                static kvp => (object)new { value = kvp.Value ?? string.Empty })
            : new Dictionary<string, object>();

        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["touser"] = openId,
            ["template_id"] = templateId,
            ["data"] = formattedData
        };

        if (!string.IsNullOrWhiteSpace(page))
        {
            payload["page"] = page;
        }

        return payload;
    }

    private PreparedTemplatePayload PreparePayloadData(
        string templateKey,
        IReadOnlyDictionary<string, string> data,
        IReadOnlyDictionary<string, string>? dynamicDefaults)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var defaults = _options.Previews.GetForTemplate(templateKey);
        var isPreviewPayload = LooksLikePreviewPayload(defaults, data);

        void Merge(IEnumerable<KeyValuePair<string, string>>? source, bool preferExisting)
        {
            if (source is null)
            {
                return;
            }

            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var trimmedValue = pair.Value?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedValue))
                {
                    continue;
                }

                var key = pair.Key.Trim();

                if (preferExisting && sanitized.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
                {
                    continue;
                }

                sanitized[key] = trimmedValue;
            }
        }

        // Populate dynamic defaults calculated on the server when available first.
        Merge(dynamicDefaults, preferExisting: false);

        // Merge request payloads, but avoid overriding dynamic data with static preview placeholders.
        if (data is not null)
        {
            foreach (var pair in data)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var key = pair.Key.Trim();
                var trimmedValue = pair.Value?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedValue))
                {
                    continue;
                }

                if (sanitized.ContainsKey(key))
                {
                    if (defaults != null && defaults.TryGetValue(key, out var defaultValue) &&
                        string.Equals(defaultValue?.Trim(), trimmedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip overriding dynamic values with placeholder defaults.
                        continue;
                    }
                }

                sanitized[key] = trimmedValue;
            }
        }

        // Fill missing fields with defaults from configuration previews.
        Merge(defaults, preferExisting: true);

        return new PreparedTemplatePayload(sanitized, isPreviewPayload);
    }

    private bool ShouldSuppressNotification(
        string templateKey,
        IReadOnlyDictionary<string, string> requestData,
        IReadOnlyDictionary<string, string>? dynamicDefaults,
        IReadOnlyDictionary<string, string> payloadData,
        bool isPreviewPayload,
        out WeChatNotificationResultDto? result)
    {
        result = null;

        if (isPreviewPayload)
        {
            return false;
        }

        if (string.Equals(templateKey, WeChatTemplateKeys.BlacklistAlert, StringComparison.OrdinalIgnoreCase))
        {
            if (!HasBlacklistPermission(requestData, dynamicDefaults))
            {
                result = new WeChatNotificationResultDto(false, -10, "当前账户无黑名单记录权限，已跳过推送");
                return true;
            }
        }
        else if (string.Equals(templateKey, WeChatTemplateKeys.SettlementNotice, StringComparison.OrdinalIgnoreCase))
        {
            if (IsZeroOrEmptyAmount(payloadData))
            {
                result = new WeChatNotificationResultDto(false, -11, "结算金额为 0，无需推送提醒");
                return true;
            }
        }

        return false;
    }

    private static bool HasBlacklistPermission(
        IReadOnlyDictionary<string, string>? requestData,
        IReadOnlyDictionary<string, string>? dynamicDefaults)
    {
        if (TryGetBooleanFlag(out var flag, requestData, dynamicDefaults, "hasBlacklistPermission", "blacklistPermission", "allowBlacklistPush", "allowBlacklistAlert"))
        {
            return flag;
        }

        return true;
    }

    private static bool IsZeroOrEmptyAmount(IReadOnlyDictionary<string, string> payloadData)
    {
        if (!TryGetValue(payloadData, "amount2", out var rawAmount))
        {
            return false;
        }

        var normalized = NormalizeAmount(rawAmount);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed == 0m;
        }

        return false;
    }

    private static string NormalizeAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch) || ch is '.' or ',')
            {
                if (ch != ',')
                {
                    builder.Append(ch);
                }
            }
            else if (builder.Length == 0 && ch == '-')
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool TryGetBooleanFlag(
        out bool flag,
        IReadOnlyDictionary<string, string>? primary,
        IReadOnlyDictionary<string, string>? secondary,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryGetValue(primary, key, out var raw) || TryGetValue(secondary, key, out raw))
            {
                if (TryParseBoolean(raw, out flag))
                {
                    return true;
                }
            }
        }

        flag = false;
        return false;
    }

    private static bool TryGetValue(
        IReadOnlyDictionary<string, string>? source,
        string key,
        out string? value)
    {
        value = null;
        if (source is null)
        {
            return false;
        }

        if (source.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var pair in source)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseBoolean(string? raw, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (bool.TryParse(normalized, out result))
        {
            return true;
        }

        normalized = normalized.ToLowerInvariant();
        if (normalized is "1" or "yes" or "y" or "on")
        {
            result = true;
            return true;
        }

        if (normalized is "0" or "no" or "n" or "off")
        {
            result = false;
            return true;
        }

        return false;
    }

    private static WeChatNotificationResultDto ParseSendResult(string responseContent)
    {
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        var errorCode = root.TryGetProperty("errcode", out var codeElement) ? codeElement.GetInt32() : 0;
        var errorMessage = root.TryGetProperty("errmsg", out var msgElement) ? msgElement.GetString() : null;
        var success = errorCode == 0;
        return new WeChatNotificationResultDto(success, errorCode, errorMessage);
    }

    private async Task<SendAttemptResult> SendOnceAsync(HttpClient client, object payload, string token, CancellationToken cancellationToken)
    {
        var requestUri = $"https://api.weixin.qq.com/cgi-bin/message/subscribe/send?access_token={token}";
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = EvaluateResponse(response, responseContent);
            var shouldRefresh = ShouldRefreshToken(response.StatusCode, result);

            return new SendAttemptResult(result, response.StatusCode, shouldRefresh);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "调用微信订阅消息接口时发生网络异常");
            var result = new WeChatNotificationResultDto(false, -6, "调用微信接口失败，请稍后重试");
            return new SendAttemptResult(result, HttpStatusCode.ServiceUnavailable, false);
        }
    }

    private WeChatNotificationResultDto EvaluateResponse(HttpResponseMessage response, string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            var errorCode = response.IsSuccessStatusCode ? -4 : (int)response.StatusCode;
            string message;

            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                message = "微信接口返回 412，请确认用户已授权订阅并模板可用";
            }
            else
            {
                message = response.IsSuccessStatusCode
                    ? "微信接口返回空响应"
                    : $"微信接口请求失败: {(int)response.StatusCode} {response.ReasonPhrase}";
            }

            _logger.LogWarning(
                "微信订阅消息调用返回空响应或 HTTP 错误：{StatusCode} {ReasonPhrase}",
                (int)response.StatusCode,
                response.ReasonPhrase);
            return new WeChatNotificationResultDto(false, errorCode, message);
        }

        try
        {
            var result = ParseSendResult(responseContent);
            if (!result.Success)
            {
                _logger.LogWarning(
                    "微信接口返回业务错误：{StatusCode} 内容：{Content}",
                    (int)response.StatusCode,
                    responseContent);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "微信接口返回了无法解析的内容: {Content}", responseContent);
            return new WeChatNotificationResultDto(false, -5, "微信接口返回无效数据");
        }
    }

    private static bool ShouldRefreshToken(HttpStatusCode statusCode, WeChatNotificationResultDto result)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return true;
        }

        // 微信常见的 access_token 失效错误码
        return result.ErrorCode is 40001 or 40014 or 42001 or 42007 or 42009;
    }

    private sealed record SendAttemptResult(WeChatNotificationResultDto Result, HttpStatusCode StatusCode, bool ShouldRefreshToken);

    private async Task LogAsync(string templateKey, string templateId, string userType, int userId, string openId, object payload, WeChatNotificationResultDto result, CancellationToken cancellationToken)
    {
        const string sql = @"INSERT INTO WeChatMessageLogs (TemplateKey, TemplateId, UserType, UserId, OpenId, PayloadJson, Success, ErrorCode, ErrorMessage, CreatedAtUtc)
VALUES (@TemplateKey, @TemplateId, @UserType, @UserId, @OpenId, @PayloadJson, @Success, @ErrorCode, @ErrorMessage, UTC_TIMESTAMP());";

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TemplateKey = templateKey,
            TemplateId = templateId,
            UserType = userType,
            UserId = userId,
            OpenId = openId,
            PayloadJson = payloadJson,
            Success = result.Success,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage
        }, cancellationToken: cancellationToken));
    }

    private string? GetTemplateId(string templateKey) => templateKey switch
    {
        WeChatTemplateKeys.InstantCommunication => _options.Templates.InstantCommunication,
        WeChatTemplateKeys.BlacklistAlert => _options.Templates.BlacklistAlert,
        WeChatTemplateKeys.SettlementNotice => _options.Templates.SettlementNotice,
        _ => null
    };
}
