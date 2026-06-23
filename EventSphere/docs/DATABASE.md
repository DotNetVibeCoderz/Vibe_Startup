# 🗄️ Database Schema

## Entity Relationship Diagram

```
┌──────────────────────┐       ┌──────────────────────┐
│    ApplicationUser   │       │       Identity       │
│     (Extends         │       │   (ASP.NET Core)     │
│   IdentityUser)      │       │                      │
├──────────────────────┤       │  IdentityRole        │
│ Id (PK)              │       │  IdentityUserRole    │
│ FullName             │       └──────────────────────┘
│ AvatarUrl            │
│ Company              │
│ Bio                  │
│ IsActive             │
│ TimeZone             │
└──────┬───────────────┘
       │
       │ 1:N ─────────────────────────────────────────┐
       │                                               │
       ▼                                               ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
│    Event     │    │ EventAttendee│    │  ChatBotSession  │
├──────────────┤    ├──────────────┤    ├──────────────────┤
│ Id (PK)      │───▶│ Id (PK)      │    │ Id (PK)          │
│ Name         │    │ EventId (FK) │    │ UserId (FK)      │
│ Description  │    │ UserId (FK)  │    │ Title            │
│ EventDate    │    │ Role         │    │ ModelProvider    │
│ EndDate      │    │ RsvpStatus   │    │ IsActive         │
│ Location     │    │ TableId (FK) │    └────────┬─────────┘
│ Theme        │    │ SeatNumber   │             │
│ Status       │    └──────────────┘             │
│ BudgetTotal  │                                  │
│ CreatedById  │    ┌──────────────┐    ┌─────────▼─────────┐
│ OrganizerId  │    │  Vendor      │    │ ChatBotMessage    │
│ ClientId     │    ├──────────────┤    ├───────────────────┤
└──────┬───────┘    │ Id (PK)      │    │ Id (PK)           │
       │            │ Name         │    │ SessionId (FK)    │
       │            │ Category     │    │ Role              │
       │ 1:N        │ Rating       │    │ Content           │
       ▼            │ PriceRange   │    │ ImageUrl          │
┌──────────────┐    │ IsVerified   │    │ AttachmentUrl     │
│ BudgetItem   │    └──────┬───────┘    │ TokenCount        │
├──────────────┤           │            └───────────────────┘
│ Id (PK)      │           │ 1:N
│ EventId (FK) │           ▼
│ Name         │    ┌──────────────┐    ┌──────────────────┐
│ Category     │    │VendorContract│    │  ChatSession     │
│ EstimatedCost│    ├──────────────┤    ├──────────────────┤
│ ActualCost   │    │ Id (PK)      │    │ Id (PK)          │
│ IsPaid       │    │ EventId (FK) │    │ Name             │
└──────────────┘    │ VendorId (FK)│    │ EventId (FK)     │
                    │ Amount       │    │ SessionType      │
┌──────────────┐    │ Status       │    └────────┬─────────┘
│  TaskItem    │    └──────┬───────┘             │
├──────────────┤           │                     │
│ Id (PK)      │           │ 1:N                 ▼
│ EventId (FK) │           ▼            ┌──────────────────┐
│ Title        │    ┌──────────────┐    │ ChatSessionMember│
│ Priority     │    │   Invoice    │    ├──────────────────┤
│ Status       │    ├──────────────┤    │ Id (PK)          │
│ DueDate      │    │ Id (PK)      │    │ SessionId (FK)   │
│ AssignedToId │    │ ContractId   │    │ UserId (FK)      │
│ Progress     │    │ Amount       │    └──────────────────┘
└──────────────┘    │ Status       │
                    └──────────────┘    ┌──────────────────┐
┌──────────────┐                        │  Notification    │
│  MediaItem   │    ┌──────────────┐    ├──────────────────┤
├──────────────┤    │  Document    │    │ Id (PK)          │
│ Id (PK)      │    ├──────────────┤    │ UserId (FK)      │
│ EventId (FK) │    │ Id (PK)      │    │ Message          │
│ Url          │    │ EventId (FK) │    │ Type             │
│ MediaType    │    │ FileName     │    │ IsRead           │
│ Category     │    │ FileUrl      │    │ EventId (FK)     │
└──────────────┘    │ FileType     │    └──────────────────┘
                    │ FileSize     │
┌──────────────┐    └──────────────┘    ┌──────────────────┐
│TableArrange. │                        │  ForumPost       │
├──────────────┤    ┌──────────────┐    ├──────────────────┤
│ Id (PK)      │    │  Feedback    │    │ Id (PK)          │
│ EventId (FK) │    ├──────────────┤    │ Title            │
│ TableName    │    │ Id (PK)      │    │ Content          │
│ Shape        │    │ EventId (FK) │    │ AuthorId (FK)    │
│ Capacity     │    │ UserId (FK)  │    │ Category         │
│ PositionX/Y  │    │ Rating       │    │ ViewCount        │
│ Color        │    │ Comment      │    │ LikeCount        │
└──────────────┘    │ Category     │    └────────┬─────────┘
                    └──────────────┘             │
                                                 ▼
┌──────────────┐                        ┌──────────────────┐
│ LoyaltyPoint │                        │  ForumComment    │
├──────────────┤                        ├──────────────────┤
│ Id (PK)      │                        │ Id (PK)          │
│ UserId (FK)  │                        │ PostId (FK)      │
│ Points       │                        │ AuthorId (FK)    │
│ Action       │                        │ Content          │
└──────────────┘                        └──────────────────┘
```

## Enums

| Enum | Values |
|------|--------|
| `EventStatus` | Draft, Planned, Confirmed, InProgress, Completed, Cancelled |
| `RsvpStatus` | Pending, Accepted, Declined, Maybe |
| `AttendeeRole` | Guest, Vip, Family, BridalParty, Speaker, Performer, Staff |
| `ContractStatus` | Pending, Sent, Signed, Active, Completed, Cancelled, Disputed |
| `InvoiceStatus` | Pending, Sent, Paid, Overdue, Cancelled |
| `TaskPriority` | Low, Medium, High, Urgent |
| `TaskItemStatus` | Todo, InProgress, Review, Done, Cancelled |

## Database Providers

| Provider | Connection String Format |
|----------|-------------------------|
| SQLite | `Data Source=EventSphere.db` |
| SQL Server | `Server=localhost;Database=EventSphere;...` |
| MySQL | `Server=localhost;Database=EventSphere;...` |
| PostgreSQL | `Host=localhost;Database=EventSphere;...` |

Configure in `appsettings.json`:
```json
{
  "Database": { "Provider": "SQLite" },
  "ConnectionStrings": { "DefaultConnection": "Data Source=EventSphere.db" }
}
```

## Indexes

| Table | Index | Purpose |
|-------|-------|---------|
| Event | EventDate | Event listing by date |
| Event | Status | Filter by status |
| Vendor | Category | Filter by category |
| TaskItem | Status, DueDate | Task filtering & deadline check |
| Notification | (UserId, IsRead) | Unread notification count |
| ChatMessage | SentAt | Message ordering |
| EventAttendee | (EventId, UserId) UNIQUE | Prevent duplicate invites |
| ChatSessionMember | (SessionId, UserId) UNIQUE | Prevent duplicate members |
