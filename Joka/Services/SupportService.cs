// Live agent support (F-6).
//
// Both sides talk to the same thread. Delivery to an open page is push, not
// polling: SupportBroadcaster raises an in-process event that every subscribed
// Blazor circuit handles, the same trick NotificationBroadcaster uses. Nothing
// here depends on that event though - the DB is the source of truth and a page
// that missed the event catches up on its next load.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Support;

namespace Joka.Services;

public record TicketResult(bool Success, string Message, SupportTicket? Ticket = null);

/// <summary>Singleton. Fan-out for live support threads.</summary>
public class SupportBroadcaster
{
    /// <summary>Raised after a message is persisted. Handlers must not throw.</summary>
    public event Func<Guid, SupportMessage, Task>? MessagePosted;

    public async Task PublishAsync(Guid ticketId, SupportMessage message)
    {
        if (MessagePosted is null) return;

        foreach (var handler in MessagePosted.GetInvocationList().Cast<Func<Guid, SupportMessage, Task>>())
        {
            try { await handler(ticketId, message); }
            catch { /* a dead circuit must not break the others */ }
        }
    }
}

public class SupportService
{
    private readonly AppDbContext _db;
    private readonly SupportBroadcaster _broadcaster;
    private readonly NotificationService _notifications;

    public SupportService(AppDbContext db, SupportBroadcaster broadcaster, NotificationService notifications)
    {
        _db = db;
        _broadcaster = broadcaster;
        _notifications = notifications;
    }

    // ------------------------------------------------------------------
    // Customer side
    // ------------------------------------------------------------------
    public async Task<TicketResult> OpenTicketAsync(
        Guid userId, string customerName, string? email,
        string subject, string category, string firstMessage, string? bookingCode)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return new(false, "Tulis dulu topik pertanyaanmu.");

        if (string.IsNullOrWhiteSpace(firstMessage) || firstMessage.Trim().Length < 10)
            return new(false, "Ceritakan masalahnya minimal 10 karakter supaya agen bisa membantu.");

        // One open thread at a time keeps the queue honest and stops a customer
        // from splitting one problem across five tickets.
        var alreadyOpen = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.UserId == userId && (t.Status == "Open" || t.Status == "Assigned"));

        if (alreadyOpen is not null)
            return new(false,
                $"Kamu masih punya tiket aktif ({alreadyOpen.TicketCode}). Lanjutkan percakapan di sana ya.",
                alreadyOpen);

        var ticket = new SupportTicket
        {
            TicketCode = $"JKA-CS-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
            UserId = userId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Pelanggan Joka" : customerName,
            CustomerEmail = email,
            Subject = subject.Trim(),
            Category = category,
            Priority = PriorityFor(category),
            RelatedBookingCode = string.IsNullOrWhiteSpace(bookingCode) ? null : bookingCode.Trim().ToUpperInvariant(),
            Status = "Open",
            LastMessageAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();

        await PostAsync(ticket.Id, "Customer", ticket.CustomerName, firstMessage);

        return new(true, $"Tiket {ticket.TicketCode} dibuat. Agen kami segera membalas.", ticket);
    }

    /// <summary>Refund and payment questions cost money to get wrong, so they jump the queue.</summary>
    private static string PriorityFor(string category) => category switch
    {
        "Refund" or "Payment" => "High",
        "Technical" => "Normal",
        _ => "Normal"
    };

    public Task<SupportTicket?> GetActiveForUserAsync(Guid userId) =>
        _db.SupportTickets
            .Include(t => t.Messages.OrderBy(m => m.SentAt))
            .Where(t => t.UserId == userId && (t.Status == "Open" || t.Status == "Assigned"))
            .OrderByDescending(t => t.LastMessageAt)
            .FirstOrDefaultAsync();

    public Task<List<SupportTicket>> GetHistoryForUserAsync(Guid userId, int take = 10) =>
        _db.SupportTickets.AsNoTracking()
            .Where(t => t.UserId == userId && (t.Status == "Resolved" || t.Status == "Closed"))
            .OrderByDescending(t => t.LastMessageAt)
            .Take(take)
            .ToListAsync();

    // ------------------------------------------------------------------
    // Shared
    // ------------------------------------------------------------------
    /// <summary>Appends a message and notifies the other side.</summary>
    public async Task<TicketResult> PostAsync(Guid ticketId, string sender, string senderName, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new(false, "Pesan tidak boleh kosong.");

        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return new(false, "Tiket tidak ditemukan.");

        if (ticket.Status is "Closed")
            return new(false, "Tiket ini sudah ditutup. Buka tiket baru untuk masalah lain.");

        var message = new SupportMessage
        {
            SupportTicketId = ticketId,
            Sender = sender,
            SenderName = senderName,
            Body = body.Trim(),
            SentAt = DateTime.UtcNow
        };

        _db.SupportMessages.Add(message);

        ticket.LastMessageAt = message.SentAt;
        ticket.UpdatedAt = message.SentAt;

        // A customer replying to a resolved thread reopens it - otherwise the
        // follow-up would sit in a closed ticket nobody looks at.
        if (sender == "Customer" && ticket.Status == "Resolved")
        {
            ticket.Status = ticket.AssignedTo is null ? "Open" : "Assigned";
            ticket.ResolvedAt = null;
        }

        await _db.SaveChangesAsync();
        await _broadcaster.PublishAsync(ticketId, message);

        // Only the agent's reply produces a bell notification; the customer's own
        // message would just be them notifying themselves.
        if (sender == "Agent")
        {
            await _notifications.SendAsync(ticket.UserId,
                $"Balasan agen — {ticket.TicketCode}",
                body.Length > 90 ? body[..90] + "…" : body,
                "Support",
                "/support");
        }

        return new(true, "Pesan terkirim.", ticket);
    }

    /// <summary>Marks the other side's messages as read.</summary>
    public async Task MarkReadAsync(Guid ticketId, string reader)
    {
        var counterpart = reader == "Agent" ? "Customer" : "Agent";

        var unread = await _db.SupportMessages
            .Where(m => m.SupportTicketId == ticketId && m.Sender == counterpart && !m.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return;

        foreach (var m in unread) m.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public Task<List<SupportMessage>> GetThreadAsync(Guid ticketId) =>
        _db.SupportMessages.AsNoTracking()
            .Where(m => m.SupportTicketId == ticketId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

    // ------------------------------------------------------------------
    // Operator side
    // ------------------------------------------------------------------
    /// <summary>
    /// The queue: unresolved first, then whoever has waited longest since their
    /// own last message.
    /// </summary>
    public Task<List<SupportTicket>> GetQueueAsync(bool includeClosed = false)
    {
        var query = _db.SupportTickets.Include(t => t.Messages).AsQueryable();

        if (!includeClosed)
            query = query.Where(t => t.Status == "Open" || t.Status == "Assigned");

        return query
            .OrderBy(t => t.Status == "Open" ? 0 : t.Status == "Assigned" ? 1 : 2)
            .ThenBy(t => t.LastMessageAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<TicketResult> AssignAsync(Guid ticketId, string agentEmail)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return new(false, "Tiket tidak ditemukan.");

        if (ticket.AssignedTo is not null && ticket.AssignedTo != agentEmail)
            return new(false, $"Tiket ini sudah ditangani {ticket.AssignedTo}.");

        ticket.AssignedTo = agentEmail;
        ticket.AssignedAt = DateTime.UtcNow;
        ticket.Status = "Assigned";
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifications.SendAsync(ticket.UserId,
            $"Agen menangani tiketmu — {ticket.TicketCode}",
            "Salah satu agen Joka sudah mengambil tiketmu dan akan segera membalas.",
            "Support",
            "/support");

        return new(true, "Tiket diambil.", ticket);
    }

    public async Task<TicketResult> ResolveAsync(Guid ticketId, string agentEmail, string? note)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return new(false, "Tiket tidak ditemukan.");

        if (string.IsNullOrWhiteSpace(note))
            return new(false, "Tulis ringkasan penyelesaiannya dulu.");

        ticket.Status = "Resolved";
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.ResolutionNote = note.Trim();
        ticket.AssignedTo ??= agentEmail;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _notifications.SendAsync(ticket.UserId,
            $"Tiket {ticket.TicketCode} selesai",
            note.Trim(),
            "Support",
            "/support");

        return new(true, "Tiket ditandai selesai.", ticket);
    }

    public async Task<TicketResult> CloseAsync(Guid ticketId)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket is null) return new(false, "Tiket tidak ditemukan.");

        ticket.Status = "Closed";
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return new(true, "Tiket ditutup.", ticket);
    }

    public Task<int> OpenCountAsync() =>
        _db.SupportTickets.CountAsync(t => t.Status == "Open");
}
