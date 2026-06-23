# 📚 API & Services Reference

## Service Dependency Injection

All services are registered as **Scoped** in `Program.cs`:
```csharp
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<VendorService>();
// ... 12 services total
```

---

## EventService

| Method | Return | Description |
|--------|--------|-------------|
| `GetAllAsync(string? userId, string? role)` | `Task<List<Event>>` | Get events filtered by user/role |
| `GetByIdAsync(Guid id)` | `Task<Event?>` | Get event with all includes |
| `CreateAsync(Event evt)` | `Task<Event>` | Create new event |
| `UpdateAsync(Event evt)` | `Task<Event?>` | Update event properties |
| `DeleteAsync(Guid id)` | `Task<bool>` | Delete event by ID |
| `UpdateBudgetAsync(Guid id)` | `Task` | Recalculate BudgetSpent from items |
| `UpdateGuestCountAsync(Guid id)` | `Task` | Recalculate ConfirmedGuests from RSVPs |
| `GetUpcomingEventsAsync(int days)` | `Task<List<Event>>` | Events within N days |

---

## VendorService

| Method | Return | Description |
|--------|--------|-------------|
| `GetAllAsync(string? category)` | `Task<List<Vendor>>` | All vendors, optional category filter |
| `GetByIdAsync(Guid id)` | `Task<Vendor?>` | Vendor with contracts, reviews, portfolios |
| `CreateAsync(Vendor v)` | `Task<Vendor>` | Add new vendor |
| `UpdateAsync(Vendor v)` | `Task<bool>` | Update vendor |
| `DeleteAsync(Guid id)` | `Task<bool>` | Delete vendor |
| `GetContractsForEventAsync(Guid)` | `Task<List<VendorContract>>` | Contracts for event |
| `CreateContractAsync(VendorContract)` | `Task<VendorContract>` | New contract |
| `AddReviewAsync(VendorReview)` | `Task<VendorReview>` | Add review + update rating |
| `GetInvoicesForContractAsync(Guid)` | `Task<List<Invoice>>` | Invoices for contract |
| `CreateInvoiceAsync(Invoice)` | `Task<Invoice>` | New invoice |

---

## GuestService

| Method | Return | Description |
|--------|--------|-------------|
| `GetAttendeesForEventAsync(Guid)` | `Task<List<EventAttendee>>` | All attendees for event |
| `InviteAsync(EventAttendee)` | `Task<EventAttendee>` | Send invitation |
| `UpdateRsvpAsync(Guid, RsvpStatus, string?)` | `Task<bool>` | Update RSVP status |
| `AssignSeatAsync(Guid, Guid, int)` | `Task<bool>` | Assign guest to table+seat |
| `RemoveAttendeeAsync(Guid)` | `Task<bool>` | Remove attendee |

---

## BudgetService

| Method | Return | Description |
|--------|--------|-------------|
| `GetItemsForEventAsync(Guid)` | `Task<List<BudgetItem>>` | All budget items for event |
| `AddItemAsync(BudgetItem)` | `Task<BudgetItem>` | Add budget item |
| `UpdateItemAsync(BudgetItem)` | `Task<bool>` | Update item |
| `DeleteItemAsync(Guid)` | `Task<bool>` | Delete item |
| `GetTotalEstimatedAsync(Guid)` | `Task<decimal>` | Sum of estimated costs |
| `GetTotalActualAsync(Guid)` | `Task<decimal>` | Sum of actual costs |

---

## TaskService

| Method | Return | Description |
|--------|--------|-------------|
| `GetTasksForEventAsync(Guid)` | `Task<List<TaskItem>>` | Tasks for specific event |
| `GetTasksForUserAsync(string)` | `Task<List<TaskItem>>` | Tasks assigned to user |
| `CreateAsync(TaskItem)` | `Task<TaskItem>` | Create task |
| `UpdateAsync(TaskItem)` | `Task<bool>` | Update task |
| `DeleteAsync(Guid)` | `Task<bool>` | Delete task |
| `MarkCompleteAsync(Guid, string)` | `Task<bool>` | Mark as done + set CompletedBy |

---

## ChatService

| Method | Return | Description |
|--------|--------|-------------|
| `GetSessionsForUserAsync(string)` | `Task<List<ChatSession>>` | User's chat sessions |
| `GetSessionAsync(Guid)` | `Task<ChatSession?>` | Session with messages+members |
| `CreateSessionAsync(ChatSession)` | `Task<ChatSession>` | New chat session |
| `SendMessageAsync(ChatMessage)` | `Task<ChatMessage>` | Send message |
| `GetMessagesForSessionAsync(Guid)` | `Task<List<ChatMessage>>` | Messages for session |
| `AddMemberAsync(Guid, string)` | `Task` | Add user to session |

---

## NotificationService

| Method | Return | Description |
|--------|--------|-------------|
| `GetNotificationsForUserAsync(string, bool)` | `Task<List<Notification>>` | User's notifications |
| `GetUnreadCountAsync(string)` | `Task<int>` | Unread count |
| `SendAsync(Notification)` | `Task<Notification>` | Send notification |
| `SendToUserAsync(string, string, string?, string?, Guid?)` | `Task` | Convenience method |
| `MarkAsReadAsync(Guid)` | `Task` | Mark single as read |
| `MarkAllAsReadAsync(string)` | `Task` | Mark all read |
| `CheckTaskDeadlinesAsync()` | `Task` | Check & notify approaching deadlines |

---

## AiChatService (Semantic Kernel)

| Method | Return | Description |
|--------|--------|-------------|
| `ChatAsync(Guid, string, string?, string?, string?, string?)` | `Task<ChatBotMessage>` | Main chat with AI (text + image + attachment) |
| `GetSessionsForUserAsync(string)` | `Task<List<ChatBotSession>>` | User's AI chat sessions |
| `CreateSessionAsync(string, string?)` | `Task<ChatBotSession>` | New chat session |
| `GetMessagesForSessionAsync(Guid)` | `Task<List<ChatBotMessage>>` | Messages in session |
| `ResetSessionAsync(Guid)` | `Task<bool>` | Clear session messages |
| `DeleteSessionAsync(Guid)` | `Task<bool>` | Soft-delete session |

### Kernel Functions (25 total)

| Category | Functions |
|----------|-----------|
| 🕐 DateTime | `get_current_datetime`, `calculate_date_difference`, `get_day_of_week`, `check_deadline_status`, `add_days_to_date` |
| 🧮 Math | `calculate_math`, `calculate_percentage`, `calculate_discount`, `calculate_average`, `calculate_ratio` |
| 📏 Conversion | `convert_currency`, `convert_units` |
| 📝 Text | `format_number`, `summarize_text` |
| 🎲 Tips | `get_random_tip`, `get_event_checklist` |
| 🌐 Internet | `search_internet` (Tavily), `scrape_webpage` |
| 🗄️ Database | `query_events`, `query_vendors`, `query_budget`, `query_tasks`, `query_seating`, `calculate_budget_estimate`, `get_dashboard_summary` |

---

## StorageService

| Method | Description |
|--------|-------------|
| `UploadAsync(Stream, string, string, string?)` | Upload file to configured provider |
| `UploadAsync(IFormFile, string?)` | Upload from form file |
| `DeleteAsync(string)` | Delete file by URL |
| `ExistsAsync(string)` | Check file existence |
| `GetPublicUrlAsync(string, TimeSpan?)` | Get presigned/public URL |

Supported providers configurable via `appsettings.json`:
- **FileSystem** (default): Local `wwwroot/uploads/`
- **AzureBlob**: Azure Blob Storage
- **S3**: AWS S3 (with MinIO-compatible endpoint)
- **MinIO**: Self-hosted MinIO

---

## ExportService

| Method | Description |
|--------|-------------|
| `ExportToCsvAsync<T>(IEnumerable<T>)` | Export to CSV bytes |
| `ExportToExcelAsync<T>(IEnumerable<T>, string)` | Export to Excel (.xlsx) bytes |
| `GenerateEventReportAsync(Guid)` | Multi-sheet Excel report |

---

## DashboardService

| Method | Return | Description |
|--------|--------|-------------|
| `GetStatsAsync()` | `Task<DashboardStats>` | Overall statistics |
| `GetMonthlyStatsAsync(int)` | `Task<List<MonthlyStats>>` | Monthly trends |
| `GetEventTypeBreakdownAsync()` | `Task<List<CategoryBreakdown>>` | Events by type |
| `GetVendorCategoryBreakdownAsync()` | `Task<List<CategoryBreakdown>>` | Vendors by category |
