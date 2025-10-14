using System;
using System.Collections.Generic;

namespace SProtectAgentWeb.Api.Dtos;

public class SendChatMessageRequest
{
    public string Software { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string? TargetUser { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MessageType { get; set; }
    public string? MediaBase64 { get; set; }
    public string? MediaName { get; set; }

}

public class CreateChatGroupRequest
{
    public string Software { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IList<string> Participants { get; set; } = new List<string>();
}

public class InviteToChatGroupRequest
{
    public string Software { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public IList<string> Participants { get; set; } = new List<string>();
}

public class ChatConversationDto
{
    public string ConversationId { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public string? GroupName { get; set; }
    public string? Owner { get; set; }
    public IList<string> Participants { get; set; } = new List<string>();
    public DateTimeOffset UpdatedAt { get; set; }
    public int UnreadCount { get; set; }
    public string? LastMessagePreview { get; set; }
}

public class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? Caption { get; set; }

}

public class ChatMessagesResponse
{
    public ChatConversationDto Conversation { get; set; } = new();
    public IList<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
}

public class ChatUnreadResponse
{
    public int Count { get; set; }
}

public class ChatSettingsResponse
{
    public int RetentionHours { get; set; }
    public bool AllowImageMessages { get; set; }
    public bool AllowEmojiPicker { get; set; }
    public int MaxImageSizeKb { get; set; }

}

public class ChatContactDto
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Remark { get; set; }
}

