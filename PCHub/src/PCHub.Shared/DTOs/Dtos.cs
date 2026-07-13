using PCHub.Shared.Enums;

namespace PCHub.Shared.DTOs;

// === AUTH DTOs ===
public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Email, string Password, string FullName, string? PhoneNumber);
public record ResetPasswordRequest(string Email);
public record ChangePasswordRequest(string OldPassword, string NewPassword);
public record AuthResponse(Guid UserId, string Username, string Email, string FullName, string Role, string Token);
public record UserProfileResponse(Guid Id, string Username, string Email, string FullName, string? PhoneNumber, UserRole Role, MembershipTier MembershipTier, int LoyaltyPoints, decimal Balance, DateTime CreatedAt);

// === PC DTOs ===
public record PcDto(Guid Id, string Name, string PcNumber, PcStatus Status, string Specifications, decimal HourlyRate, double? CpuUsage, double? GpuUsage, double? RamUsage, bool IsActive);
public record PcCreateRequest(string Name, string PcNumber, string Specifications, decimal HourlyRate);
public record PcUpdateRequest(Guid Id, string Name, string PcNumber, PcStatus Status, string Specifications, decimal HourlyRate, bool IsActive);

// === GAME DTOs ===
public record GameDto(Guid Id, string Name, GameGenre Genre, string? Description, string? ExecutablePath, string? IconUrl, string? CoverImageUrl, bool IsInstalled, string? Version, bool IsPopular);
public record GameCreateRequest(string Name, GameGenre Genre, string? Description, string? ExecutablePath, string? IconUrl, string? Version);
public record GameUpdateRequest(Guid Id, string Name, GameGenre Genre, string? Description, string? ExecutablePath, string? IconUrl, string? Version, bool IsPopular);

// === BILLING DTOs ===
public record BillingDto(Guid Id, Guid UserId, string Username, Guid PcId, string PcName, DateTime StartTime, DateTime? EndTime, decimal HourlyRate, decimal TotalCost, BillingStatus Status, PaymentMethod PaymentMethod, PaymentStatus PaymentStatus);
public record StartBillingRequest(Guid UserId, Guid PcId, PaymentMethod PaymentMethod);
public record StopBillingRequest(Guid BillingId);

// === RESERVATION DTOs ===
public record ReservationDto(Guid Id, Guid UserId, string Username, Guid? PcId, string? PcName, DateTime ReservationDate, int DurationMinutes, string? GameRequested, ReservationStatus Status, string? Notes);
public record CreateReservationRequest(Guid? PcId, DateTime ReservationDate, int DurationMinutes, string? GameRequested, string? Notes);

// === MEMBERSHIP DTOs ===
public record MembershipDto(Guid Id, string Name, MembershipTier Tier, string? Description, decimal MonthlyPrice, int DiscountPercentage, int BonusHours, int LoyaltyPointsPerMonth, bool IsActive);
public record MembershipCreateRequest(string Name, MembershipTier Tier, string? Description, decimal MonthlyPrice, int DiscountPercentage, int BonusHours, int LoyaltyPointsPerMonth);
public record SubscribeMembershipRequest(Guid MembershipId, int DurationMonths);

// === PROMO DTOs ===
public record PromoDto(Guid Id, string Name, string? Description, string? PromoCode, int DiscountPercentage, decimal? MaxDiscount, DateTime StartDate, DateTime EndDate, bool IsActive);
public record PromoCreateRequest(string Name, string? Description, string? PromoCode, int DiscountPercentage, decimal? MaxDiscount, DateTime StartDate, DateTime EndDate);

// === TOURNAMENT DTOs ===
public record TournamentDto(Guid Id, string Name, string? Description, Guid? GameId, string? GameName, DateTime StartDate, DateTime EndDate, int MaxParticipants, decimal EntryFee, decimal PrizePool, bool IsActive, int ParticipantCount);
public record TournamentCreateRequest(string Name, string? Description, Guid? GameId, DateTime StartDate, DateTime EndDate, int MaxParticipants, decimal EntryFee, decimal PrizePool);
public record JoinTournamentRequest(Guid TournamentId);

// === TOURNAMENT BRACKET DTOs ===
public record TournamentBracketDto(Guid TournamentId, string TournamentName, List<BracketRoundDto> Rounds);
public record BracketRoundDto(int RoundNumber, string Name, List<BracketMatchDto> Matches);
public record BracketMatchDto(Guid Id, string? Player1, string? Player2, int? Score1, int? Score2, Guid? WinnerId, bool IsCompleted);

// === SUPPORT DTOs ===
public record SupportTicketDto(Guid Id, Guid UserId, string Username, string Subject, string Message, string Status, DateTime CreatedAt, int ReplyCount);
public record CreateTicketRequest(string Subject, string Message);
public record ReplyTicketRequest(Guid TicketId, string Message);

// === DASHBOARD DTOs ===
public record DashboardStats(int TotalUsers, int ActiveUsers, int TotalPcs, int AvailablePcs, decimal TodayRevenue, decimal MonthRevenue, int ActiveSessions, int PendingReservations, List<PopularGameStat> PopularGames, List<DailyRevenue> RevenueChart);
public record PopularGameStat(string GameName, int PlayCount, int TotalMinutes);
public record DailyRevenue(DateTime Date, decimal Amount);

// === PAGING DTOs ===
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize) { public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize); public bool HasPrevious => Page > 1; public bool HasNext => Page < TotalPages; }
public record PagingRequest(int Page = 1, int PageSize = 10, string? Search = null, string? SortBy = null, bool SortDesc = false);

// === CHAT BOT DTOs ===
public record ChatMessageDto(Guid SessionId, string Role, string Content, DateTime Timestamp, string? ImageUrl = null, string? DocumentUrl = null);
public record ChatSessionDto(Guid Id, string Title, DateTime CreatedAt, int MessageCount);
public record CreateChatSessionRequest(string? Title = null);
public record SendChatMessageRequest(Guid SessionId, string Message, string? ImageUrl = null, string? DocumentUrl = null);
public record ChatBotSettings(string? Provider = "LocalRule", string? Model = "gpt-4o-mini", double Temperature = 0.7, string? SystemPrompt = null, string? ApiKey = null, string? Endpoint = null, int MaxTokens = 2048, int MaxHistoryMessages = 20, bool FallbackToLocalRule = true, string? TavilyApiKey = null);

// === NOTIFICATION DTOs ===
public record NotificationDto(Guid Id, string Title, string Message, NotificationType Type, bool IsRead, DateTime CreatedAt);
public record SendNotificationRequest(Guid? UserId, string Title, string Message, NotificationType Type, NotificationChannel Channel);

// === CONFIG DTOs ===
public record SystemConfigDto(Guid Id, string Key, string Value, string? Description);
public record UpdateConfigRequest(string Key, string Value);
public record AppSettingsDto(string DatabaseProvider, string ConnectionString, string StorageProvider, string StorageConnectionString, string SmtpServer, int SmtpPort, string SmtpUsername, string SmtpPassword, string WhatsAppApiKey, ChatBotSettings ChatBot);

// === PAYMENT DTOs ===
public record PaymentRequest(Guid BillingId, decimal Amount, PaymentMethod Method, string? PromoCode = null);
public record PaymentResponse(Guid PaymentId, string TransactionId, string Status, string? PaymentUrl, decimal Amount);
public record CreatePaymentRequest(Guid BillingId, PaymentMethod Method, decimal Amount);

// === EMAIL DTOs ===
public record EmailRequest(string To, string Subject, string Body, bool IsHtml = true, List<EmailAttachment>? Attachments = null);
public record EmailAttachment(string FileName, byte[] Content, string ContentType);

// === IoT DTOs ===
public record IoTDeviceDto(string DeviceId, string Name, IoTDeviceType Type, bool IsOnline, string? Status);
public record IoTCommandRequest(string DeviceId, string Command, string? Parameters);

// === REDIS CACHE DTOs ===
public record CacheStats(long Hits, long Misses, int ConnectedClients, long UsedMemoryBytes);
