namespace SmartDrive.Models.Enums;

/// <summary>
/// Role pengguna dalam sistem
/// </summary>
public enum UserRole
{
    Admin,
    Instructor,
    Student
}

/// <summary>
/// Status pembayaran
/// </summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    Expired
}

/// <summary>
/// Metode pembayaran
/// </summary>
public enum PaymentMethod
{
    BankTransfer,
    EWallet,
    CreditCard,
    Cash
}

/// <summary>
/// Status booking jadwal
/// </summary>
public enum BookingStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    NoShow
}

/// <summary>
/// Status kendaraan
/// </summary>
public enum VehicleStatus
{
    Available,
    InUse,
    Maintenance,
    Repair,
    Retired
}

/// <summary>
/// Jenis transmisi kendaraan
/// </summary>
public enum TransmissionType
{
    Manual,
    Automatic
}

/// <summary>
/// Level badge gamifikasi
/// </summary>
public enum BadgeLevel
{
    Bronze,
    Silver,
    Gold,
    Platinum,
    Diamond
}

/// <summary>
/// Jenis notifikasi
/// </summary>
public enum NotificationType
{
    ScheduleReminder,
    PaymentReminder,
    ExamReminder,
    FeedbackReceived,
    GeneralInfo,
    SystemAlert
}

/// <summary>
/// Status order marketplace
/// </summary>
public enum OrderStatus
{
    Cart,
    Pending,
    Confirmed,
    Processing,
    Completed,
    Cancelled
}

/// <summary>
/// Jenis kelamin
/// </summary>
public enum Gender
{
    Male,
    Female
}

/// <summary>
/// Status servis kendaraan
/// </summary>
public enum ServiceStatus
{
    Scheduled,
    InProgress,
    Completed,
    Overdue
}

/// <summary>
/// Database provider yang didukung
/// </summary>
public enum DatabaseProvider
{
    SQLite,
    SQLServer,
    MySQL,
    PostgreSQL
}

/// <summary>
/// Storage provider yang didukung
/// </summary>
public enum StorageProvider
{
    FileSystem,
    AzureBlob,
    S3,
    MinIO
}

/// <summary>
/// AI model provider
/// </summary>
public enum AIProvider
{
    OpenAI,
    Anthropic,
    Gemini,
    Ollama
}
