namespace FitnessCenter.Models;

public enum MembershipStatus { Active, Expired, Suspended, Cancelled, Pending }
public enum MembershipDuration { Daily, Weekly, Monthly, Quarterly, Yearly, Lifetime }
public enum PaymentMethod { Cash, CreditCard, DebitCard, EWallet, BankTransfer, QRIS }

/// <summary>
/// Pending = menunggu pembayaran | Confirmed = member klaim sudah bayar | Completed = admin verifikasi
/// </summary>
public enum PaymentStatus { Pending, Confirmed, Completed, Failed, Refunded, Cancelled }

public enum ClassType { Yoga, Zumba, HIIT, Pilates, Boxing, Spinning, Aerobics, Strength, Dance, MartialArts, Meditation, Swimming }
public enum ClassLevel { Beginner, Intermediate, Advanced, AllLevels }
public enum AttendanceType { CheckIn, CheckOut }
public enum NotificationType { ClassReminder, PaymentReminder, Promotion, Motivation, System, Emergency, Event }
public enum UserRole { Admin, Trainer, Member, Staff }
public enum Gender { Male, Female, Other }
public enum DiscountType { Percentage, FixedAmount }
public enum DiscountScope { All, MembershipPlan, Class, Product }
public enum FeedbackType { Class, Trainer, Facility, General }
public enum EventStatus { Draft, Published, Ongoing, Completed, Cancelled }
public enum ChallengeStatus { Active, Completed, Upcoming }
public enum AchievementCategory { Attendance, Workout, Class, Social, Streak, Special }
public enum StorageProvider { FileSystem, AzureBlob, S3, MinIO }
public enum DatabaseProvider { SQLite, SQLServer, MySQL, PostgreSQL }
public enum AIModelProvider { OpenAI, Anthropic, Gemini, Ollama }
