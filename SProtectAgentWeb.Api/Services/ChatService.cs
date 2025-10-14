using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Configuration;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Dtos;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Services;

public class ChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly AppConfig _config;
    private readonly DatabaseManager _databaseManager;
    private readonly PermissionHelper _permissionHelper;
    private readonly ILogger<ChatService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private const string TextMessageType = "text";
    private const string ImageMessageType = "image";
    private static readonly IReadOnlyDictionary<string, string> ImageContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    public ChatService(AppConfig config, DatabaseManager databaseManager, PermissionHelper permissionHelper, ILogger<ChatService> logger)
    {
        _config = config;
        _databaseManager = databaseManager;
        _permissionHelper = permissionHelper;
        _logger = logger;
    }

    public async Task<ChatMessagesResponse> SendDirectMessageAsync(
        string software,
        Agent currentAgent,
        string targetUsername,
        string content,
        string? messageType = null,
        string? mediaBase64 = null,
        string? mediaName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentNullException.ThrowIfNull(currentAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUsername);
        var normalizedType = NormalizeMessageType(messageType);
        EnsureMessageTypeSupported(normalizedType);

        content = (content ?? string.Empty).Trim();
        if (normalizedType == TextMessageType && string.IsNullOrEmpty(content))
        {
            throw new InvalidOperationException("消息内容不能为空");
        }

        var targetAgent = await RequireAgentAsync(software, targetUsername).ConfigureAwait(false);
        EnsureAgentActive(targetAgent);

        if (!AreAgentsRelated(currentAgent, targetAgent))
        {
            throw new InvalidOperationException("仅允许与上下级代理互相聊天");
        }

        var conversationId = BuildDirectConversationId(currentAgent.User, targetAgent.User);
        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            if (metadata is null)
            {
                metadata = new ChatConversationMetadata
                {
                    Id = conversationId,
                    Software = software,
                    IsGroup = false,
                    Owner = DetermineConversationOwner(currentAgent, targetAgent),
                    GroupName = null,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LastMessagePreview = null,
                };
            }

            EnsureParticipant(metadata, currentAgent.User, now, resetState: metadata.Participants.Count == 0);
            EnsureParticipant(metadata, targetAgent.User, now, resetState: metadata.Participants.Count <= 1);

            var message = await AppendMessageInternalAsync(
                software,
                metadata,
                currentAgent.User,
                content,
                normalizedType,
                now,
                mediaBase64,
                mediaName).ConfigureAwait(false);
            await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);
            return BuildMessagesResponse(metadata, new[] { message }, currentAgent.User);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatMessagesResponse> SendMessageToConversationAsync(
        string software,
        Agent currentAgent,
        string conversationId,
        string content,
        string? messageType = null,
        string? mediaBase64 = null,
        string? mediaName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentNullException.ThrowIfNull(currentAgent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        var normalizedType = NormalizeMessageType(messageType);
        EnsureMessageTypeSupported(normalizedType);

        content = (content ?? string.Empty).Trim();
        if (normalizedType == TextMessageType && string.IsNullOrEmpty(content))
        {
            throw new InvalidOperationException("消息内容不能为空");
        }

        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("会话不存在");

            if (!ContainsParticipant(metadata, currentAgent.User))
            {
                throw new InvalidOperationException("您无权发送该会话的消息");
            }

            var message = await AppendMessageInternalAsync(
                software,
                metadata,
                currentAgent.User,
                content,
                normalizedType,
                DateTimeOffset.UtcNow,
                mediaBase64,
                mediaName).ConfigureAwait(false);
            await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);
            return BuildMessagesResponse(metadata, new[] { message }, currentAgent.User);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatMessagesResponse> GetMessagesAsync(string software, string conversationId, string username, DateTimeOffset? after, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("会话不存在");

            if (!ContainsParticipant(metadata, username))
            {
                throw new InvalidOperationException("您无权查看该会话");
            }

            var messages = await ReadMessagesInternalAsync(software, conversationId).ConfigureAwait(false);
            if (after.HasValue)
            {
                messages = messages.Where(m => m.Timestamp > after.Value).ToList();
            }

            if (limit > 0 && messages.Count > limit)
            {
                messages = messages.Skip(messages.Count - limit).ToList();
            }

            if (messages.Count > 0)
            {
                var lastTimestamp = messages[^1].Timestamp;
                var state = EnsureParticipant(metadata, username, lastTimestamp);
                state.LastRead = lastTimestamp;
                state.UnreadCount = 0;
                await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);
            }

            return BuildMessagesResponse(metadata, messages, username);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatAttachmentDescriptor?> OpenAttachmentAsync(string software, string conversationId, string username, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("会话不存在");

            if (!ContainsParticipant(metadata, username))
            {
                throw new InvalidOperationException("您无权查看该会话");
            }

            var path = GetAttachmentPath(software, conversationId, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var extension = Path.GetExtension(path);
            if (!ImageContentTypes.TryGetValue(extension, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new ChatAttachmentDescriptor(stream, contentType, Path.GetFileName(path));
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatConversationDto>> GetConversationsAsync(string software, string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var folder = GetSoftwareFolder(software);
        if (!Directory.Exists(folder))
        {
            return Array.Empty<ChatConversationDto>();
        }

        var results = new List<ChatConversationDto>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
        {
            var conversationId = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                continue;
            }

            var lockKey = BuildLockKey(software, conversationId);
            var gate = GetLock(lockKey);
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false);
                if (metadata is null || !ContainsParticipant(metadata, username))
                {
                    continue;
                }

                var unread = metadata.ParticipantStates
                    .FirstOrDefault(s => string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase))?.UnreadCount ?? 0;

                results.Add(new ChatConversationDto
                {
                    ConversationId = metadata.Id,
                    IsGroup = metadata.IsGroup,
                    GroupName = metadata.GroupName,
                    Owner = metadata.Owner,
                    Participants = metadata.Participants.ToList(),
                    UpdatedAt = metadata.UpdatedAt,
                    UnreadCount = unread,
                    LastMessagePreview = metadata.LastMessagePreview,
                });
            }
            finally
            {
                gate.Release();
            }
        }

        return results
            .OrderByDescending(r => r.UpdatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ChatContactDto>> GetContactsAsync(string software, Agent currentAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentNullException.ThrowIfNull(currentAgent);

        if (!SqliteBridge.IsNativeAvailable)
        {
            throw new DllNotFoundException("未找到 sp_sqlite_bridge 原生库，无法加载代理数据");
        }

        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        return await Task.Run(() =>
        {
            var records = SqliteBridge.GetAgents(dbPath);
            var contacts = new List<ChatContactDto>();
            foreach (var record in records)
            {
                if (record.DeletedAt != 0 || record.Stat == 1)
                {
                    continue;
                }

                if (string.Equals(record.User, currentAgent.User, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!_permissionHelper.IsChildAgent(record.FNode, currentAgent.User))
                {
                    continue;
                }

                var remark = record.Remarks ?? string.Empty;
                var displayName = string.IsNullOrWhiteSpace(remark)
                    ? record.User
                    : $"{record.User}（{remark}）";

                contacts.Add(new ChatContactDto
                {
                    Username = record.User,
                    Remark = remark,
                    DisplayName = displayName,
                });
            }

            return (IReadOnlyList<ChatContactDto>)contacts
                .OrderBy(contact => contact.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }).ConfigureAwait(false);
    }

    public async Task<ChatConversationDto> CreateGroupAsync(string software, Agent creator, string groupName, IEnumerable<string> participants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentNullException.ThrowIfNull(creator);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);

        var participantList = participants?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        var invitedAgents = new List<Agent>();
        foreach (var participant in participantList)
        {
            if (string.Equals(participant, creator.User, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var agent = await RequireAgentAsync(software, participant).ConfigureAwait(false);
            EnsureAgentActive(agent);
            if (!_permissionHelper.IsChildAgent(agent.FNode, creator.User))
            {
                throw new InvalidOperationException($"只能邀请自己的下级代理：{participant}");
            }

            invitedAgents.Add(agent);
        }

        var conversationId = $"group_{Guid.NewGuid():N}";
        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var metadata = new ChatConversationMetadata
            {
                Id = conversationId,
                Software = software,
                IsGroup = true,
                GroupName = groupName.Trim(),
                Owner = creator.User,
                CreatedAt = now,
                UpdatedAt = now,
                LastMessagePreview = null,
            };

            EnsureParticipant(metadata, creator.User, now, resetState: true);
            foreach (var agent in invitedAgents)
            {
                EnsureParticipant(metadata, agent.User, now, resetState: true);
            }

            await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);
            return new ChatConversationDto
            {
                ConversationId = metadata.Id,
                IsGroup = metadata.IsGroup,
                GroupName = metadata.GroupName,
                Owner = metadata.Owner,
                Participants = metadata.Participants.ToList(),
                UpdatedAt = metadata.UpdatedAt,
                UnreadCount = 0,
                LastMessagePreview = metadata.LastMessagePreview,
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatConversationDto> InviteToGroupAsync(string software, Agent inviter, string conversationId, IEnumerable<string> participants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(software);
        ArgumentNullException.ThrowIfNull(inviter);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var normalizedParticipants = participants?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalizedParticipants.Count == 0)
        {
            throw new InvalidOperationException("请至少邀请一位代理");
        }

        var lockKey = BuildLockKey(software, conversationId);
        var gate = GetLock(lockKey);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("群聊不存在");

            if (!metadata.IsGroup)
            {
                throw new InvalidOperationException("仅群聊支持邀请功能");
            }

            if (!ContainsParticipant(metadata, inviter.User))
            {
                throw new InvalidOperationException("您不在该群聊中");
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var name in normalizedParticipants)
            {
                if (ContainsParticipant(metadata, name))
                {
                    continue;
                }

                var agent = await RequireAgentAsync(software, name).ConfigureAwait(false);
                EnsureAgentActive(agent);
                if (!_permissionHelper.IsChildAgent(agent.FNode, inviter.User))
                {
                    throw new InvalidOperationException($"只能邀请自己的下级代理：{name}");
                }

                EnsureParticipant(metadata, agent.User, now, resetState: true);
            }

            metadata.UpdatedAt = now;
            await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);

            return new ChatConversationDto
            {
                ConversationId = metadata.Id,
                IsGroup = metadata.IsGroup,
                GroupName = metadata.GroupName,
                Owner = metadata.Owner,
                Participants = metadata.Participants.ToList(),
                UpdatedAt = metadata.UpdatedAt,
                UnreadCount = metadata.ParticipantStates
                    .FirstOrDefault(s => string.Equals(s.Username, inviter.User, StringComparison.OrdinalIgnoreCase))?.UnreadCount ?? 0,
                LastMessagePreview = metadata.LastMessagePreview,
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<int> GetUnreadCountAsync(string software, string username)
    {
        var conversations = await GetConversationsAsync(software, username).ConfigureAwait(false);
        return conversations.Sum(c => c.UnreadCount);
    }

    public async Task<int> GetUnreadTotalAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.SoftwareAgentInfo.Count == 0)
        {
            return 0;
        }

        var pairs = session.SoftwareAgentInfo
            .Select(pair => (Software: pair.Key, Username: pair.Value.User))
            .ToList();

        var tasks = pairs.Select(pair => GetUnreadCountAsync(pair.Software, pair.Username));
        var counts = await Task.WhenAll(tasks).ConfigureAwait(false);
        return counts.Sum();
    }

    public int GetRetentionHours()
    {
        return Math.Max(1, _config.Chat?.RetentionHours ?? 24);
    }

    public ChatSettingsResponse GetSettings()
    {
        return new ChatSettingsResponse
        {
            RetentionHours = GetRetentionHours(),
            AllowImageMessages = IsImageMessagingEnabled(),
            AllowEmojiPicker = IsEmojiPickerEnabled(),
            MaxImageSizeKb = GetMaxImageSizeKb(),
        };
    }

    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var retentionHours = GetRetentionHours();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-retentionHours);

        string root;
        try
        {
            root = GetChatRootPath();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "跳过聊天清理：未配置数据目录");
            return;
        }

        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var softwareDir in Directory.EnumerateDirectories(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var software = Path.GetFileName(softwareDir) ?? string.Empty;
            foreach (var metadataFile in Directory.EnumerateFiles(softwareDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var conversationId = Path.GetFileNameWithoutExtension(metadataFile);
                if (string.IsNullOrWhiteSpace(conversationId))
                {
                    continue;
                }

                var lockKey = BuildLockKey(software, conversationId);
                var gate = GetLock(lockKey);
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var metadata = await LoadMetadataInternalAsync(software, conversationId).ConfigureAwait(false);
                    if (metadata is null)
                    {
                        continue;
                    }

                    var messages = await ReadMessagesInternalAsync(software, conversationId).ConfigureAwait(false);
                    var filtered = messages.Where(m => m.Timestamp >= cutoff).ToList();

                    if (filtered.Count != messages.Count)
                    {
                        await RewriteMessagesInternalAsync(software, conversationId, filtered).ConfigureAwait(false);
                    }

                    if (filtered.Count > 0)
                    {
                        metadata.UpdatedAt = filtered[^1].Timestamp;
                        metadata.LastMessagePreview = filtered[^1].Type == ImageMessageType
                            ? BuildImagePreview(filtered[^1].Caption)
                            : Truncate(filtered[^1].Content);
                    }
                    else
                    {
                        metadata.LastMessagePreview = null;
                        metadata.UpdatedAt = metadata.CreatedAt;
                    }

                    foreach (var state in metadata.ParticipantStates)
                    {
                        if (state.LastRead < cutoff)
                        {
                            state.LastRead = cutoff;
                        }

                        state.UnreadCount = filtered.Count(m => m.Timestamp > state.LastRead);
                    }

                    await SaveMetadataInternalAsync(software, metadata).ConfigureAwait(false);
                    CleanupAttachments(software, conversationId, filtered);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "清理聊天记录失败：{Software}/{Conversation}", software, conversationId);
                }
                finally
                {
                    gate.Release();
                }
            }
        }
    }

    private string DetermineConversationOwner(Agent current, Agent target)
    {
        if (string.Equals(current.User, target.User, StringComparison.OrdinalIgnoreCase))
        {
            return current.User;
        }

        if (_permissionHelper.IsChildAgent(target.FNode, current.User))
        {
            return current.User;
        }

        if (_permissionHelper.IsChildAgent(current.FNode, target.User))
        {
            return target.User;
        }

        return current.User;
    }

    private async Task<Agent> RequireAgentAsync(string software, string username)
    {
        if (!SqliteBridge.IsNativeAvailable)
        {
            throw new DllNotFoundException("未找到 sp_sqlite_bridge 原生库，无法加载代理数据");
        }

        var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
        var record = await Task.Run(() => SqliteBridge.GetAgent(dbPath, username)).ConfigureAwait(false);
        if (record is null)
        {
            throw new InvalidOperationException($"代理 {username} 不存在");
        }

        return new Agent
        {
            User = record.Value.User,
            Authority = record.Value.Authority,
            FNode = record.Value.FNode,
            Stat = record.Value.Stat,
            Deltm = record.Value.DeletedAt,
        };
    }

    private static void EnsureAgentActive(Agent agent)
    {
        if (agent.Deltm != 0 || agent.Stat == 1)
        {
            throw new InvalidOperationException($"代理 {agent.User} 当前不可用");
        }
    }

    private bool AreAgentsRelated(Agent a, Agent b)
    {
        if (string.Equals(a.User, b.User, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _permissionHelper.IsChildAgent(a.FNode, b.User)
               || _permissionHelper.IsChildAgent(b.FNode, a.User);
    }

    private ChatParticipantState EnsureParticipant(ChatConversationMetadata metadata, string username, DateTimeOffset defaultTimestamp, bool resetState = false)
    {
        if (!ContainsParticipant(metadata, username))
        {
            metadata.Participants.Add(username);
            resetState = true;
        }

        var state = metadata.ParticipantStates
            .FirstOrDefault(s => string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase));
        if (state is null)
        {
            state = new ChatParticipantState { Username = username };
            metadata.ParticipantStates.Add(state);
            resetState = true;
        }

        if (resetState)
        {
            state.LastRead = defaultTimestamp;
            state.UnreadCount = 0;
        }

        return state;
    }

    private async Task<ChatMessage> AppendMessageInternalAsync(
        string software,
        ChatConversationMetadata metadata,
        string sender,
        string content,
        string messageType,
        DateTimeOffset timestamp,
        string? mediaBase64 = null,
        string? mediaName = null)
    {
        foreach (var participant in metadata.Participants.ToList())
        {
            EnsureParticipant(metadata, participant, metadata.UpdatedAt);
        }

        EnsureParticipant(metadata, sender, timestamp);

        var normalizedType = NormalizeMessageType(messageType);
        var storedContent = normalizedType == ImageMessageType
            ? SaveImageAttachment(software, metadata.Id, mediaBase64, mediaName)
            : content.Trim();

        string? caption = null;
        if (normalizedType == ImageMessageType)
        {
            caption = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString("n"),
            Sender = sender,
            Content = storedContent,
            Caption = caption,
            Type = normalizedType,
            Timestamp = timestamp,
        };

        await AppendMessageToFileAsync(software, metadata.Id, message).ConfigureAwait(false);
        metadata.UpdatedAt = timestamp;
        metadata.LastMessagePreview = message.Type == ImageMessageType
            ? BuildImagePreview(message.Caption)
            : Truncate(message.Content);

        foreach (var state in metadata.ParticipantStates)
        {
            if (string.Equals(state.Username, sender, StringComparison.OrdinalIgnoreCase))
            {
                state.LastRead = timestamp;
                state.UnreadCount = 0;
            }
            else
            {
                state.UnreadCount = Math.Max(0, state.UnreadCount + 1);
            }
        }

        return message;
    }

    private async Task AppendMessageToFileAsync(string software, string conversationId, ChatMessage message)
    {
        var path = GetMessagePath(software, conversationId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = JsonSerializer.Serialize(message, JsonOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(path, payload, Utf8NoBom).ConfigureAwait(false);
    }

    private async Task RewriteMessagesInternalAsync(string software, string conversationId, IList<ChatMessage> messages)
    {
        var path = GetMessagePath(software, conversationId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, Utf8NoBom);
        foreach (var message in messages)
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            await writer.WriteLineAsync(json).ConfigureAwait(false);
        }
    }

    private void CleanupAttachments(string software, string conversationId, IReadOnlyCollection<ChatMessage> messages)
    {
        var folder = GetAttachmentFolder(software, conversationId, ensureExists: false);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        var referenced = new HashSet<string>(messages
            .Where(m => string.Equals(m.Type, ImageMessageType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m.Content.Trim()), StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var name = Path.GetFileName(file);
            if (string.IsNullOrEmpty(name) || referenced.Contains(name))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "删除过期聊天附件失败：{File}", file);
            }
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder, false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "清理聊天附件目录失败：{Folder}", folder);
        }
    }

    private async Task<List<ChatMessage>> ReadMessagesInternalAsync(string software, string conversationId)
    {
        var path = GetMessagePath(software, conversationId);
        if (!File.Exists(path))
        {
            return new List<ChatMessage>();
        }

        var result = new List<ChatMessage>();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Utf8NoBom);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(line, JsonOptions);
                if (message != null)
                {
                    message.Type = NormalizeMessageType(message.Type);
                    if (message.Type != ImageMessageType)
                    {
                        message.Caption = null;
                    }
                    result.Add(message);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析聊天消息失败：{Conversation}", conversationId);
            }
        }

        return result;
    }

    private async Task<ChatConversationMetadata?> LoadMetadataInternalAsync(string software, string conversationId)
    {
        var path = GetMetadataPath(software, conversationId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, Utf8NoBom).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ChatConversationMetadata>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "解析聊天元数据失败：{Software}/{Conversation}", software, conversationId);
            return null;
        }
    }

    private Task SaveMetadataInternalAsync(string software, ChatConversationMetadata metadata)
    {
        var path = GetMetadataPath(software, metadata.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        return File.WriteAllTextAsync(path, json, Utf8NoBom);
    }

    private string GetMetadataPath(string software, string conversationId)
    {
        var folder = GetSoftwareFolder(software);
        return Path.Combine(folder, $"{conversationId}.json");
    }

    private string GetMessagePath(string software, string conversationId)
    {
        var folder = GetSoftwareFolder(software);
        return Path.Combine(folder, $"{conversationId}.jsonl");
    }

    private string GetAttachmentFolder(string software, string conversationId, bool ensureExists = true)
    {
        var folder = Path.Combine(GetSoftwareFolder(software), "attachments", SanitizeSegment(conversationId));
        if (ensureExists)
        {
            Directory.CreateDirectory(folder);
        }

        return folder;
    }

    private string GetAttachmentPath(string software, string conversationId, string fileName)
    {
        var folder = GetAttachmentFolder(software, conversationId, ensureExists: false);
        if (string.IsNullOrEmpty(folder))
        {
            throw new InvalidOperationException("聊天附件目录不存在");
        }

        var sanitized = SanitizeFileName(fileName);
        return Path.Combine(folder, sanitized);
    }

    private string GetSoftwareFolder(string software)
    {
        var root = GetChatRootPath();
        var segment = SanitizeSegment(software);
        var path = Path.Combine(root, segment);
        Directory.CreateDirectory(path);
        return path;
    }

    private string GetChatRootPath()
    {
        var dataPath = _config.GetDataPath();
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new InvalidOperationException("未配置聊天数据目录");
        }

        var root = Path.Combine(dataPath, "chats");
        Directory.CreateDirectory(root);
        return root;
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "default";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '-' or '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var result = builder.ToString();
        return string.IsNullOrWhiteSpace(result) ? "default" : result;
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Guid.NewGuid().ToString("n");
        }

        var builder = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('_');
            }
        }

        var sanitized = builder.ToString();
        return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("n") : sanitized;
    }

    private string SaveImageAttachment(string software, string conversationId, string? mediaBase64, string? mediaName)
    {
        if (!IsImageMessagingEnabled())
        {
            throw new InvalidOperationException("当前未开启图片消息功能");
        }

        if (string.IsNullOrWhiteSpace(mediaBase64))
        {
            throw new InvalidOperationException("缺少图片数据");
        }

        var data = mediaBase64.Trim();
        string? extension = null;
        if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = data.IndexOf(',');
            if (commaIndex <= 0)
            {
                throw new InvalidOperationException("图片数据格式无效");
            }

            var meta = data[..commaIndex];
            extension = GuessExtensionFromDataUrlMeta(meta);
            data = data[(commaIndex + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("图片数据格式无效", ex);
        }

        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("图片数据为空");
        }

        var maxBytes = GetMaxImageSizeBytes();
        if (bytes.Length > maxBytes)
        {
            throw new InvalidOperationException($"图片大小不能超过 {GetMaxImageSizeKb()} KB");
        }

        extension ??= GuessExtensionFromSignature(bytes);
        extension ??= GuessExtensionFromFileName(mediaName);
        extension ??= ".png";

        if (!ImageContentTypes.ContainsKey(extension))
        {
            extension = ".png";
        }

        var folder = GetAttachmentFolder(software, conversationId);
        var fileName = BuildAttachmentFileName(extension);
        var path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, bytes);
        return fileName;
    }

    private static string BuildImagePreview(string? caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
        {
            return "[图片]";
        }

        return Truncate($"[图片] {caption.Trim()}");
    }

    private static string? GuessExtensionFromDataUrlMeta(string meta)
    {
        var lower = meta.ToLowerInvariant();
        if (!lower.StartsWith("data:image/"))
        {
            return null;
        }

        var start = lower.IndexOf("image/", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += "image/".Length;
        var end = lower.IndexOf(';', start);
        if (end < 0)
        {
            end = lower.Length;
        }

        var subtype = lower[start..end];
        return subtype switch
        {
            "jpeg" or "jpg" => ".jpg",
            "png" => ".png",
            "gif" => ".gif",
            "webp" => ".webp",
            _ => null,
        };
    }

    private static string? GuessExtensionFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        extension = extension.ToLowerInvariant();
        return ImageContentTypes.ContainsKey(extension) ? extension : null;
    }

    private static string? GuessExtensionFromSignature(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
        {
            return ".png";
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8)
        {
            return ".jpg";
        }

        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return ".gif";
        }

        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return ".webp";
        }

        return null;
    }

    private static string BuildAttachmentFileName(string extension)
    {
        extension = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var suffix = Guid.NewGuid().ToString("n")[..8];
        return $"{timestamp}_{suffix}{extension}";
    }

    private static string NormalizeMessageType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return TextMessageType;
        }

        var normalized = type.Trim().ToLowerInvariant();
        return normalized == ImageMessageType ? ImageMessageType : TextMessageType;
    }

    private void EnsureMessageTypeSupported(string messageType)
    {
        if (messageType == ImageMessageType && !IsImageMessagingEnabled())
        {
            throw new InvalidOperationException("当前未开启图片消息功能");
        }

        if (messageType != TextMessageType && messageType != ImageMessageType)
        {
            throw new InvalidOperationException($"不支持的消息类型：{messageType}");
        }
    }

    private bool IsImageMessagingEnabled() => _config.Chat?.EnableImageMessages ?? false;

    private bool IsEmojiPickerEnabled() => _config.Chat?.EnableEmojiPicker ?? true;

    private int GetMaxImageSizeKb() => Math.Clamp(_config.Chat?.MaxImageSizeKb ?? 2048, 32, 10240);

    private int GetMaxImageSizeBytes() => GetMaxImageSizeKb() * 1024;

    private static string BuildDirectConversationId(string userA, string userB)
    {
        var normalized = new[]
        {
            NormalizeUsername(userA),
            NormalizeUsername(userB),
        };
        Array.Sort(normalized, StringComparer.Ordinal);
        var key = string.Join('|', normalized);
        var hash = HashIdentifier(key);
        return $"direct_{hash}";
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static string HashIdentifier(string value)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
    }

    private string BuildLockKey(string software, string conversationId)
    {
        return $"{SanitizeSegment(software)}::{conversationId}";
    }

    private SemaphoreSlim GetLock(string key)
    {
        return _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }

    private static bool ContainsParticipant(ChatConversationMetadata metadata, string username)
    {
        return metadata.Participants.Any(p => string.Equals(p, username, StringComparison.OrdinalIgnoreCase));
    }

    private static ChatMessagesResponse BuildMessagesResponse(ChatConversationMetadata metadata, IEnumerable<ChatMessage> messages, string username)
    {
        var messageList = messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            Timestamp = m.Timestamp,
            Sender = m.Sender,
            Content = m.Content,
            Type = m.Type,
            Caption = m.Caption,
        }).ToList();

        var state = metadata.ParticipantStates
            .FirstOrDefault(s => string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase));

        var dto = new ChatConversationDto
        {
            ConversationId = metadata.Id,
            IsGroup = metadata.IsGroup,
            GroupName = metadata.GroupName,
            Owner = metadata.Owner,
            Participants = metadata.Participants.ToList(),
            UpdatedAt = metadata.UpdatedAt,
            UnreadCount = state?.UnreadCount ?? 0,
            LastMessagePreview = metadata.LastMessagePreview,
        };

        return new ChatMessagesResponse
        {
            Conversation = dto,
            Messages = messageList,
        };
    }

    private static string? Truncate(string? content, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var normalized = content.Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength] + "…";
    }
}