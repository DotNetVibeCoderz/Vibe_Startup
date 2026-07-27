// Live agent support: a ticket with a message thread on it.
//
// Deliberately separate from IncidentReport - that one is an operator writing
// about a system problem, this one is a conversation with a customer.
using Joka.Models.Common;

namespace Joka.Models.Support;

public class SupportTicket : BaseEntity
{
    /// <summary>Short human-quotable reference, e.g. JKA-CS-260727-4821.</summary>
    public string TicketCode { get; set; } = string.Empty;

    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";   // General, Booking, Payment, Refund, Technical
    public string Priority { get; set; } = "Normal";    // Low, Normal, High, Urgent

    /// <summary>Open, Assigned, Resolved, Closed.</summary>
    public string Status { get; set; } = "Open";

    /// <summary>Email of the operator handling it. Null while unclaimed.</summary>
    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }

    /// <summary>Booking this is about, when the customer gave one.</summary>
    public string? RelatedBookingCode { get; set; }

    /// <summary>Drives the queue ordering, so the oldest untouched thread floats up.</summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}

public class SupportMessage : BaseEntity
{
    public Guid SupportTicketId { get; set; }
    public SupportTicket? Ticket { get; set; }

    /// <summary>Customer or Agent. Drives which side of the thread it renders on.</summary>
    public string Sender { get; set; } = "Customer";

    public string SenderName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Read by the other side. Used for the unread badge in the queue.</summary>
    public bool IsRead { get; set; }
}
