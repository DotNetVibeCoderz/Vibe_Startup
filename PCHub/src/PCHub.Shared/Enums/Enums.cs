using System.Text.Json.Serialization;

namespace PCHub.Shared.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PcStatus { Available, InUse, Maintenance, Broken, Reserved }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserRole { Admin, Operator, Member, Guest }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod { Cash, EWallet, BankTransfer, Qris, Membership, Midtrans, Xendit }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus { Pending, Completed, Failed, Refunded }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReservationStatus { Pending, Confirmed, Cancelled, Completed, NoShow }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MembershipTier { Basic, Silver, Gold, Platinum, VIP }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingStatus { Active, Paused, Completed, Locked }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameGenre { FPS, MOBA, RPG, Racing, Sport, Simulation, BattleRoyale, Strategy, Horror, Adventure, Other }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiProvider { OpenAI, Anthropic, Gemini, Ollama, LocalRule }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType { Booking, Promo, Reminder, System, Support }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel { Email, WhatsApp, InApp, SMS, SignalR }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StorageProvider { FileSystem, AzureBlob, S3, MinIO }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DatabaseProvider { SQLite, SqlServer, PostgreSQL, MySQL }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IoTDeviceType { SmartLamp, SmartAC, SmartLock, Sensor, Relay, Other }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TournamentBracketType { SingleElimination, DoubleElimination, RoundRobin, Swiss }
