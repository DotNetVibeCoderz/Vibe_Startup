using Microsoft.AspNetCore.Identity;

namespace EventSphere.Data.Models;

/// <summary>
/// Extended Identity user dengan role matrix: Admin, Organizer, Client, Vendor, Guest, Moderator
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Company { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? TimeZone { get; set; } = "Asia/Jakarta";
    
    // Navigation
    public ICollection<EventAttendee> AttendeeEvents { get; set; } = new List<EventAttendee>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<Document> UploadedDocuments { get; set; } = new List<Document>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
