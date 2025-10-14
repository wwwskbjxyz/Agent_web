using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SProtectAgentWeb.Api.Services;

public class ChatCleanupBackgroundService : BackgroundService
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatCleanupBackgroundService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

    public ChatCleanupBackgroundService(ChatService chatService, ILogger<ChatCleanupBackgroundService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _chatService.CleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "聊天记录清理任务执行失败");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
