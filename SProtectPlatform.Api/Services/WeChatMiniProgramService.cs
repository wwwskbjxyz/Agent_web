using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SProtectPlatform.Api.Options;

namespace SProtectPlatform.Api.Services;

public interface IWeChatMiniProgramService
{
    Task<WeChatSessionInfo> CodeToSessionAsync(string jsCode, CancellationToken cancellationToken);
}

public sealed class WeChatMiniProgramService : IWeChatMiniProgramService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeChatMiniProgramService> _logger;
    private readonly WeChatOptions _options;

    public WeChatMiniProgramService(IHttpClientFactory httpClientFactory, IOptions<WeChatOptions> options, ILogger<WeChatMiniProgramService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<WeChatSessionInfo> CodeToSessionAsync(string jsCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppSecret))
        {
            throw new InvalidOperationException("未配置 WeChat:AppId 或 WeChat:AppSecret。");
        }

        var client = _httpClientFactory.CreateClient(nameof(IWeChatMiniProgramService));
        var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={_options.AppId}&secret={_options.AppSecret}&js_code={jsCode}&grant_type=authorization_code";
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("errcode", out var codeElement) && codeElement.GetInt32() != 0)
        {
            var errorCode = codeElement.GetInt32();
            var errorMessage = root.TryGetProperty("errmsg", out var errmsg) ? errmsg.GetString() : string.Empty;
            _logger.LogWarning("调用微信 jscode2session 失败：{Code} {Message}", errorCode, errorMessage);
            throw new InvalidOperationException($"微信登录失败：{errorCode} {errorMessage}");
        }

        var openId = root.GetProperty("openid").GetString();
        var unionId = root.TryGetProperty("unionid", out var unionElement) ? unionElement.GetString() : null;

        if (string.IsNullOrWhiteSpace(openId))
        {
            throw new InvalidOperationException("微信未返回 openid");
        }

        return new WeChatSessionInfo(openId, unionId);
    }
}

public sealed record WeChatSessionInfo(string OpenId, string? UnionId);
