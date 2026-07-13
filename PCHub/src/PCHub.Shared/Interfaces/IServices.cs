using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Models;

namespace PCHub.Shared.Interfaces;

// === CORE ===
public interface IAuthService { Task<AuthResponse?> LoginAsync(LoginRequest r); Task<AuthResponse?> RegisterAsync(RegisterRequest r); Task<bool> ResetPasswordAsync(ResetPasswordRequest r); Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest r); Task<UserProfileResponse?> GetProfileAsync(Guid userId); }
public interface IPcService { Task<List<PcDto>> GetAllPcsAsync(); Task<PcDto?> GetPcByIdAsync(Guid id); Task<PagedResult<PcDto>> GetPcsPagedAsync(PagingRequest r); Task<PcDto> CreatePcAsync(PcCreateRequest r); Task<PcDto> UpdatePcAsync(PcUpdateRequest r); Task DeletePcAsync(Guid id); Task UpdatePcStatusAsync(Guid id, PcStatus s); Task<PcDto> UpdatePcResourceAsync(Guid id, double cpu, double gpu, double ram); }
public interface IGameService { Task<List<GameDto>> GetAllGamesAsync(); Task<PagedResult<GameDto>> GetGamesPagedAsync(PagingRequest r); Task<GameDto> CreateGameAsync(GameCreateRequest r); Task<GameDto> UpdateGameAsync(GameUpdateRequest r); Task DeleteGameAsync(Guid id); }
public interface IBillingService { Task<BillingDto> StartBillingAsync(StartBillingRequest r); Task<BillingDto> StopBillingAsync(Guid id); Task<BillingDto?> GetActiveBillingAsync(Guid userId); Task<List<BillingDto>> GetUserBillingHistoryAsync(Guid userId); Task<PagedResult<BillingDto>> GetAllBillingPagedAsync(PagingRequest r); Task<decimal> CalculateCostAsync(Guid id); }
public interface IReservationService { Task<List<ReservationDto>> GetUserReservationsAsync(Guid userId); Task<PagedResult<ReservationDto>> GetAllReservationsPagedAsync(PagingRequest r); Task<ReservationDto> CreateReservationAsync(Guid userId, CreateReservationRequest r); Task<ReservationDto> UpdateReservationStatusAsync(Guid id, ReservationStatus s); Task CancelReservationAsync(Guid id); }
public interface IMembershipService { Task<List<MembershipDto>> GetAllMembershipsAsync(); Task<MembershipDto> CreateMembershipAsync(MembershipCreateRequest r); Task<UserMembership> SubscribeAsync(Guid userId, SubscribeMembershipRequest r); Task CheckAndUpdateMembershipStatusAsync(); }
public interface IPromoService { Task<List<PromoDto>> GetActivePromosAsync(); Task<PagedResult<PromoDto>> GetAllPromosPagedAsync(PagingRequest r); Task<PromoDto> CreatePromoAsync(PromoCreateRequest r); Task<bool> ValidatePromoCodeAsync(string code); }
public interface IDashboardService { Task<DashboardStats> GetDashboardStatsAsync(); }
public interface ITournamentService { Task<List<TournamentDto>> GetActiveTournamentsAsync(); Task<PagedResult<TournamentDto>> GetAllTournamentsPagedAsync(PagingRequest r); Task<TournamentDto> CreateTournamentAsync(TournamentCreateRequest r); Task JoinTournamentAsync(Guid userId, JoinTournamentRequest r); Task<TournamentBracketDto?> GetBracketAsync(Guid tournamentId); Task UpdateMatchResultAsync(Guid matchId, int score1, int score2); }
public interface INotificationService { Task SendNotificationAsync(SendNotificationRequest r); Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId); Task MarkAsReadAsync(Guid id); Task BroadcastAsync(string title, string message, NotificationType type); }
public interface IExportService { Task<byte[]> ExportToCsvAsync<T>(List<T> data); Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheet = "Data"); Task<byte[]> ExportToPdfAsync<T>(List<T> data, string title = "Report", string? subtitle = null); }

// === AI ===
public interface IChatBotService { Task<ChatMessageDto> SendMessageAsync(SendChatMessageRequest r); Task<List<ChatSessionDto>> GetSessionsAsync(); Task<ChatSessionDto> CreateSessionAsync(string? title = null); Task DeleteSessionAsync(Guid id); Task ResetSessionAsync(Guid id); Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid id); void UpdateSettings(ChatBotSettings s); }

// === STORAGE ===
public interface IStorageService { Task<string> UploadFileAsync(string name, byte[] data, string mime); Task<byte[]> DownloadFileAsync(string path); Task<bool> DeleteFileAsync(string path); }

// === EMAIL ===
public interface IEmailService { Task<bool> SendEmailAsync(EmailRequest r); Task<bool> SendBookingConfirmationAsync(string to, string userName, DateTime bookingDate, string pcName); Task<bool> SendPaymentReceiptAsync(string to, string userName, decimal amount, string transactionId); }

// === PAYMENT ===
public interface IPaymentService { Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest r); Task<PaymentResponse> CheckPaymentStatusAsync(string transactionId); Task<bool> ProcessRefundAsync(string transactionId, decimal amount); }

// === CACHE ===
public interface ICacheService { Task<T?> GetAsync<T>(string key); Task SetAsync<T>(string key, T value, TimeSpan? expiry = null); Task RemoveAsync(string key); Task<bool> ExistsAsync(string key); Task<CacheStats> GetStatsAsync(); }
