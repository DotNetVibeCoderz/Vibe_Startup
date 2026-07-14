using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace BlazePoint.Data;

public class ApplicationUser : IdentityUser
{
    [MaxLength(128)] public string DisplayName { get; set; } = "";
    [MaxLength(16)] public string AvatarColor { get; set; } = "#1877f2";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Team Sites ----------
public class Site
{
    public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(128)] public string Slug { get; set; } = "";
    [MaxLength(512)] public string Description { get; set; } = "";
    [MaxLength(128)] public string Department { get; set; } = "";
    [MaxLength(16)] public string Color { get; set; } = "#1877f2";
    [MaxLength(8)] public string Icon { get; set; } = "🏢";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Navigation ----------
public enum NavLocation { TopNav = 0, QuickLaunch = 1 }

public class NavigationItem
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(128)] public string Title { get; set; } = "";
    [MaxLength(512)] public string Url { get; set; } = "";
    [MaxLength(32)] public string Icon { get; set; } = "";
    public int Order { get; set; }
    public NavLocation Location { get; set; }
    public int? ParentId { get; set; }
    public bool RequiresAuth { get; set; }
}

// ---------- CMS Pages & WebParts ----------
public class CmsPage
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(256)] public string Title { get; set; } = "";
    [MaxLength(256)] public string Slug { get; set; } = "";
    [MaxLength(64)] public string Layout { get; set; } = "Default"; // Default | News | Intranet | Web
    public string ContentJson { get; set; } = "[]"; // draft webpart sections
    public string PublishedJson { get; set; } = "[]"; // published version
    public bool IsPublished { get; set; }
    public int Version { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CmsPageVersion> Versions { get; set; } = [];
}

public class CmsPageVersion
{
    public int Id { get; set; }
    public int PageId { get; set; }
    public int Version { get; set; }
    public string ContentJson { get; set; } = "[]";
    [MaxLength(256)] public string Comment { get; set; } = "";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// A webpart instance inside a page (serialized into ContentJson)
public class WebPartModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Type { get; set; } = "Text"; // registered webpart type key
    public string Title { get; set; } = "";
    public int Column { get; set; } // 0..2
    public int Order { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}

// ---------- Documents ----------
public class Document
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(512)] public string FolderPath { get; set; } = "/"; // virtual folder
    [MaxLength(256)] public string Name { get; set; } = "";
    [MaxLength(128)] public string ContentType { get; set; } = "";
    public long Size { get; set; }
    [MaxLength(512)] public string StorageKey { get; set; } = "";
    public int Version { get; set; } = 1;
    public string MetadataJson { get; set; } = "{}"; // tags, category, custom fields
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DocumentVersion> Versions { get; set; } = [];
}

public class DocumentVersion
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int Version { get; set; }
    [MaxLength(512)] public string StorageKey { get; set; } = "";
    public long Size { get; set; }
    [MaxLength(512)] public string Comment { get; set; } = "";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Lists & Libraries ----------
public class ListDefinition
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(512)] public string Description { get; set; } = "";
    [MaxLength(8)] public string Icon { get; set; } = "📋";
    public string ColumnsJson { get; set; } = "[]"; // List<ListColumn>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ListItemEntity> Items { get; set; } = [];
}

public class ListColumn
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Text"; // Text | Number | Date | Choice | Boolean | User | Url
    public bool Required { get; set; }
    public List<string> Choices { get; set; } = [];
    public bool ShowInView { get; set; } = true;
}

public class ListItemEntity
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string ValuesJson { get; set; } = "{}";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Workflow ----------
public class WorkflowDefinition
{
    public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(512)] public string Description { get; set; } = "";
    public string DefinitionJson { get; set; } = """{"nodes":[],"edges":[]}""";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum WorkflowStatus { Running = 0, Completed = 1, Rejected = 2, Cancelled = 3 }

public class WorkflowInstance
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public WorkflowDefinition? Definition { get; set; }
    [MaxLength(256)] public string Subject { get; set; } = "";
    public WorkflowStatus Status { get; set; }
    [MaxLength(64)] public string CurrentNodeId { get; set; } = "";
    public string ContextJson { get; set; } = "{}";
    public string? StartedById { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<ApprovalTask> Tasks { get; set; } = [];
}

public enum ApprovalStatus { Pending = 0, Approved = 1, Rejected = 2 }

public class ApprovalTask
{
    public int Id { get; set; }
    public int InstanceId { get; set; }
    public WorkflowInstance? Instance { get; set; }
    [MaxLength(64)] public string NodeId { get; set; } = "";
    [MaxLength(256)] public string Title { get; set; } = "";
    public string? AssignedToId { get; set; }
    public ApprovalStatus Status { get; set; }
    [MaxLength(1024)] public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

// ---------- Notifications ----------
public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    [MaxLength(256)] public string Title { get; set; } = "";
    [MaxLength(1024)] public string Message { get; set; } = "";
    [MaxLength(512)] public string Link { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Discussions ----------
public class DiscussionThread
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(256)] public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<DiscussionPost> Posts { get; set; } = [];
}

public class DiscussionPost
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int? ParentPostId { get; set; }
    public string Body { get; set; } = "";
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Calendar ----------
public class CalendarEvent
{
    public int Id { get; set; }
    public int? SiteId { get; set; }
    [MaxLength(256)] public string Title { get; set; } = "";
    [MaxLength(1024)] public string Description { get; set; } = "";
    [MaxLength(256)] public string Location { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool AllDay { get; set; }
    [MaxLength(16)] public string Color { get; set; } = "#1877f2";
    public int ReminderMinutes { get; set; } = 0; // 0 = none
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- File Sharing ----------
public class ShareLink
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document? Document { get; set; }
    [MaxLength(64)] public string Token { get; set; } = "";
    public bool IsPublic { get; set; } = true; // false = requires login
    public DateTime? ExpiresAt { get; set; }
    public int DownloadCount { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Forms ----------
public class FormDefinition
{
    public int Id { get; set; }
    [MaxLength(128)] public string Name { get; set; } = "";
    [MaxLength(512)] public string Description { get; set; } = "";
    public string SchemaJson { get; set; } = "[]"; // List<FormField>
    public bool IsTemplate { get; set; }
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<FormSubmission> Submissions { get; set; } = [];
}

public class FormField
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text"; // text | textarea | number | date | select | checkbox | radio | email
    public bool Required { get; set; }
    public string Placeholder { get; set; } = "";
    public List<string> Options { get; set; } = [];
}

public class FormSubmission
{
    public int Id { get; set; }
    public int FormId { get; set; }
    public string DataJson { get; set; } = "{}";
    public string? SubmittedById { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Clippy Chat ----------
public class ChatSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    [MaxLength(256)] public string Title { get; set; } = "Percakapan baru";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessageEntity> Messages { get; set; } = [];
}

public class ChatMessageEntity
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    [MaxLength(16)] public string Role { get; set; } = "user"; // user | assistant
    public string Content { get; set; } = "";
    public string AttachmentsJson { get; set; } = "[]"; // List<ChatAttachment>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatAttachment
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public bool IsImage => ContentType.StartsWith("image/");
}

// ---------- Search Index ----------
public class SearchIndexEntry
{
    public int Id { get; set; }
    [MaxLength(32)] public string EntityType { get; set; } = ""; // Document | Page | ListItem | Discussion
    public int EntityId { get; set; }
    [MaxLength(512)] public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    [MaxLength(512)] public string Link { get; set; } = "";
    public byte[]? Embedding { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Admin / Monitoring ----------
public class AuditLog
{
    public int Id { get; set; }
    [MaxLength(64)] public string Category { get; set; } = ""; // Auth | Document | Page | Workflow | Chat | System
    [MaxLength(1024)] public string Message { get; set; } = "";
    public string? UserId { get; set; }
    [MaxLength(128)] public string UserName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AppSetting
{
    [Key, MaxLength(128)] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
