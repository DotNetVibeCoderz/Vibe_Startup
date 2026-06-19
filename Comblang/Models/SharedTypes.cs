namespace Comblang.Models;

/// <summary>
/// Lightweight DTO for displaying a user card in swipe / discovery views.
/// </summary>
public class UserProfileCard
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int Age { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string[] Interests { get; set; } = Array.Empty<string>();
    public double CompatibilityScore { get; set; }
    public bool IsPremium { get; set; }
    public bool IsVerified { get; set; }
}

/// <summary>
/// Result of a swipe action indicating whether a mutual match was created.
/// </summary>
public class MatchResult
{
    public bool IsMatch { get; set; }
    public Guid? MatchId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string MatchedUserName { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight DTO for chat list items in the inbox.
/// </summary>
public class ChatItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Avatar { get; set; } = "👤";
    public string LastMessage { get; set; } = string.Empty;
    public int Unread { get; set; }
    public DateTime LastMessageAt { get; set; }
    public bool IsOnline { get; set; }
    public bool IsGroup { get; set; }
}

/// <summary>
/// DTO for a single chat message rendered in the conversation view.
/// </summary>
public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public bool IsMine { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
    public string? MediaUrl { get; set; }
    public bool IsRead { get; set; }
}

/// <summary>
/// Lightweight DTO for group room discovery and listing.
/// </summary>
public class GroupRoomItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "💬";
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public int MemberCount { get; set; }
    public int MessageCount { get; set; }
    public bool IsJoined { get; set; }
}

/// <summary>
/// Lightweight DTO for displaying gift catalog items.
/// </summary>
public class GiftItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "🎁";
    public string IconUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Price { get; set; }
    public string Category { get; set; } = "General";
}

/// <summary>
/// Lightweight DTO for displaying an event in listings.
/// </summary>
public class EventItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🎉";
    public string Type { get; set; } = "Online";
    public string Description { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string BgColor { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public int MaxParticipants { get; set; }
    public bool IsJoined { get; set; }
    public bool IsActive { get; set; }
}
