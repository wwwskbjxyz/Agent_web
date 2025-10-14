using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace SProtectAgentWeb.Api.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string? Caption { get; set; }

}

public class ChatConversationMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Software { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public string? GroupName { get; set; }
    public string Owner { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
    public List<ChatParticipantState> ParticipantStates { get; set; } = new();
    public long CreatedAtUnix { get; set; }
    public long UpdatedAtUnix { get; set; }
    public string? LastMessagePreview { get; set; }

    [JsonIgnore]
    public DateTimeOffset CreatedAt
    {
        get => CreatedAtUnix <= 0 ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnix);
        set => CreatedAtUnix = value.ToUnixTimeSeconds();
    }

    [JsonIgnore]
    public DateTimeOffset UpdatedAt
    {
        get => UpdatedAtUnix <= 0 ? CreatedAt : DateTimeOffset.FromUnixTimeSeconds(UpdatedAtUnix);
        set => UpdatedAtUnix = value.ToUnixTimeSeconds();
    }
}

public class ChatParticipantState
{
    public string Username { get; set; } = string.Empty;
    public long LastReadUnix { get; set; }
    public int UnreadCount { get; set; }

    [JsonIgnore]
    public DateTimeOffset LastRead
    {
        get => LastReadUnix <= 0 ? DateTimeOffset.UnixEpoch : DateTimeOffset.FromUnixTimeSeconds(LastReadUnix);
        set => LastReadUnix = value.ToUnixTimeSeconds();
    }
}


public record ChatAttachmentDescriptor(Stream Stream, string ContentType, string FileName);

