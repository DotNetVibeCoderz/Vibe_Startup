using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services.ChatBot;

/// <summary>
/// Kernel Functions untuk query data dari database FitnessCenter.
/// Memungkinkan AI ChatBot mengakses data member, kelas, trainer, dll.
/// </summary>
public class DatabaseQueryPlugin
{
    private readonly AppDbContext _db;

    public DatabaseQueryPlugin(AppDbContext db) => _db = db;

    // ---- MEMBER QUERIES ----

    [KernelFunction("get_member_count")]
    [Description("Mendapatkan jumlah total member aktif di FitnessCenter")]
    public async Task<int> GetMemberCountAsync()
    {
        return await _db.Users.CountAsync(u => u.IsActive);
    }

    [KernelFunction("get_member_by_name")]
    [Description("Mencari member berdasarkan nama. Return detail member termasuk points dan membership expiry.")]
    public async Task<string> GetMemberByNameAsync(
        [Description("Nama member yang dicari")] string name)
    {
        var members = await _db.Users
            .Where(u => u.FullName.Contains(name) && u.IsActive)
            .Select(u => new
            {
                u.FullName, u.Email, u.PhoneNumber, u.Role,
                u.LoyaltyPoints, u.MembershipExpiryDate, u.RegisteredAt
            })
            .Take(5)
            .ToListAsync();

        if (!members.Any()) return $"Tidak ditemukan member dengan nama '{name}'.";

        return string.Join("\n\n", members.Select(m =>
            $"👤 {m.FullName}\n" +
            $"   📧 Email: {m.Email}\n" +
            $"   📱 Phone: {m.PhoneNumber}\n" +
            $"   ⭐ Points: {m.LoyaltyPoints}\n" +
            $"   🗓️ Member since: {m.RegisteredAt:dd MMM yyyy}\n" +
            $"   📅 Membership expires: {(m.MembershipExpiryDate?.ToString("dd MMM yyyy") ?? "N/A")}"));
    }

    [KernelFunction("get_member_stats")]
    [Description("Mendapatkan statistik lengkap member. Total, active, inactive, dan retention rate.")]
    public async Task<string> GetMemberStatsAsync()
    {
        var total = await _db.Users.CountAsync();
        var active = await _db.Users.CountAsync(u => u.IsActive);
        var expired = await _db.Users.CountAsync(u => u.MembershipExpiryDate < DateTime.UtcNow && u.IsActive);
        var newThisMonth = await _db.Users.CountAsync(u => u.RegisteredAt >= DateTime.UtcNow.AddMonths(-1));

        return $"📊 Statistik Member FitnessCenter:\n" +
               $"   👥 Total: {total}\n" +
               $"   ✅ Active: {active}\n" +
               $"   ⚠️ Expired: {expired}\n" +
               $"   🆕 New this month: {newThisMonth}\n" +
               $"   📈 Retention: {(total > 0 ? Math.Round((double)active / total * 100, 1) : 0)}%";
    }

    // ---- CLASS QUERIES ----

    [KernelFunction("get_classes_today")]
    [Description("Mendapatkan jadwal kelas untuk hari ini")]
    public async Task<string> GetClassesTodayAsync()
    {
        try
        {
            var today = DateTime.UtcNow.DayOfWeek;

            // SQLite tidak support ORDER BY TimeSpan → materialize dulu, sort di memory
            var schedules = await _db.ClassSchedules
                .Include(s => s.FitnessClass)
                .ThenInclude(c => c!.Trainer)
                .Where(s => s.DayOfWeek == today && !s.IsCancelled)
                .ToListAsync(); // materialize — hindari TimeSpan di SQL

            // Sort di memory (client-side)
            var ordered = schedules.OrderBy(s => s.StartTime).ToList();

            if (!ordered.Any()) return "Tidak ada kelas terjadwal hari ini.";

            return "📅 Jadwal Kelas Hari Ini:\n\n" + string.Join("\n", ordered.Select(s =>
                $"   🕐 {s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm} | {s.FitnessClass?.Name}\n" +
                $"      👤 Trainer: {s.FitnessClass?.Trainer?.FullName}\n" +
                $"      📍 {s.FitnessClass?.Room} | 👥 {s.CurrentBookings}/{s.FitnessClass?.MaxParticipants}\n" +
                $"      🏷️ {s.FitnessClass?.Type} | 📊 {s.FitnessClass?.Level}"));
        }
        catch (Exception ex)
        {
            return $"Terjadi kesalahan saat mengambil jadwal: {ex.Message}";
        }
    }

    [KernelFunction("get_class_by_type")]
    [Description("Mencari kelas berdasarkan tipe (Yoga, Zumba, HIIT, dll)")]
    public async Task<string> GetClassByTypeAsync(
        [Description("Tipe kelas: Yoga, Zumba, HIIT, Pilates, Boxing, Spinning, Aerobics, Strength, Dance, MartialArts, Meditation, Swimming")] string classType)
    {
        if (!Enum.TryParse<ClassType>(classType, true, out var type))
        {
            var validTypes = string.Join(", ", Enum.GetNames<ClassType>());
            return $"Tipe kelas '{classType}' tidak valid. Tipe yang tersedia: {validTypes}";
        }

        var classes = await _db.FitnessClasses
            .Include(c => c.Trainer)
            .Include(c => c.Schedules)
            .Where(c => c.Type == type && c.IsActive)
            .ToListAsync();

        if (!classes.Any()) return $"Tidak ada kelas {type} yang tersedia saat ini.";

        return $"🎯 Kelas {type}:\n\n" + string.Join("\n\n", classes.Select(c =>
            $"   📛 {c.Name}\n" +
            $"   👤 Trainer: {c.Trainer?.FullName}\n" +
            $"   ⏱️ Durasi: {c.Duration.TotalMinutes} menit\n" +
            $"   📊 Level: {c.Level}\n" +
            $"   👥 Max: {c.MaxParticipants} peserta\n" +
            $"   📍 {c.Room}" +
            (c.Schedules.Any() ? $"\n   📅 Jadwal: {string.Join(", ", c.Schedules.Select(s => $"{s.DayOfWeek} {s.StartTime:hh\\:mm}"))}" : "")));
    }

    // ---- TRAINER QUERIES ----

    [KernelFunction("get_trainers")]
    [Description("Mendapatkan daftar semua trainer aktif beserta rating dan spesialisasi")]
    public async Task<string> GetTrainersAsync()
    {
        var trainers = await _db.Trainers
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.Rating)
            .ToListAsync();

        if (!trainers.Any()) return "Tidak ada trainer yang tersedia.";

        return "🎯 Daftar Trainer FitnessCenter:\n\n" + string.Join("\n", trainers.Select((t, i) =>
            $"   {i + 1}. 👤 {t.FullName}\n" +
            $"      🎯 {t.Specialization}\n" +
            $"      ⭐ Rating: {t.Rating:F1}/5.0\n" +
            $"      📚 Kelas diajar: {t.TotalClasses}\n" +
            $"      📝 {t.Bio?.Truncate(100)}"));
    }

    [KernelFunction("get_trainer_schedule")]
    [Description("Mendapatkan jadwal trainer tertentu berdasarkan nama")]
    public async Task<string> GetTrainerScheduleAsync(
        [Description("Nama trainer")] string trainerName)
    {
        var trainer = await _db.Trainers
            .Include(t => t.Classes)
            .ThenInclude(c => c.Schedules)
            .FirstOrDefaultAsync(t => t.FullName.Contains(trainerName) && t.IsActive);

        if (trainer == null) return $"Trainer dengan nama '{trainerName}' tidak ditemukan.";

        // Sudah materialized lewat Include → sort di memory aman untuk TimeSpan
        var schedules = trainer.Classes
            .Where(c => c.IsActive)
            .SelectMany(c => c.Schedules.Select(s => new { Class = c, Schedule = s }))
            .OrderBy(x => x.Schedule.DayOfWeek)
            .ThenBy(x => x.Schedule.StartTime)  // client-side, aman
            .ToList();

        if (!schedules.Any()) return $"Trainer {trainer.FullName} belum memiliki jadwal kelas.";

        return $"📅 Jadwal {trainer.FullName} ({trainer.Specialization}):\n\n" +
               string.Join("\n", schedules.Select(x =>
                   $"   📛 {x.Class.Name} | 📅 {x.Schedule.DayOfWeek} | 🕐 {x.Schedule.StartTime:hh\\:mm}-{x.Schedule.EndTime:hh\\:mm} | 📍 {x.Class.Room}"));
    }

    // ---- MEMBERSHIP QUERIES ----

    [KernelFunction("get_membership_plans")]
    [Description("Mendapatkan daftar semua paket membership beserta harga")]
    public async Task<string> GetMembershipPlansAsync()
    {
        var plans = await _db.MembershipPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

        return "💳 Paket Membership FitnessCenter:\n\n" + string.Join("\n\n", plans.Select(p =>
            $"   📛 {p.Name}\n" +
            $"   💰 Rp {p.Price:N0}" +
            (p.DiscountedPrice.HasValue ? $" (Disc: Rp {p.DiscountedPrice.Value:N0})" : "") + "\n" +
            $"   ⏱️ Durasi: {p.Duration}\n" +
            $"   📅 Max {p.MaxClassesPerMonth} kelas/bulan\n" +
            $"   🔄 Auto-renew: {(p.AllowAutoRenew ? "Ya" : "Tidak")}\n" +
            $"   👤 Personal Trainer: {(p.IncludesPersonalTrainer ? "✅" : "❌")}\n" +
            $"   🥗 Nutrition Plan: {(p.IncludesNutritionPlan ? "✅" : "❌")}\n" +
            $"   📝 {p.Description}"));
    }

    // ---- PAYMENT QUERIES ----

    [KernelFunction("get_revenue_today")]
    [Description("Mendapatkan total pendapatan hari ini")]
    public async Task<string> GetRevenueTodayAsync()
    {
        var today = DateTime.UtcNow.Date;
        var total = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt.HasValue && p.PaidAt.Value.Date == today)
            .SumAsync(p => p.Amount);
        var count = await _db.Payments
            .CountAsync(p => p.Status == PaymentStatus.Completed && p.PaidAt.HasValue && p.PaidAt.Value.Date == today);

        return $"💰 Pendapatan Hari Ini:\n   Total: Rp {total:N0}\n   Transaksi: {count}";
    }

    // ---- EVENTS ----

    [KernelFunction("get_upcoming_events")]
    [Description("Mendapatkan daftar event yang akan datang")]
    public async Task<string> GetUpcomingEventsAsync()
    {
        var events = await _db.Events
            .Where(e => e.EventDate >= DateTime.UtcNow && (e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing))
            .OrderBy(e => e.EventDate)
            .Take(5)
            .ToListAsync();

        if (!events.Any()) return "Tidak ada event yang akan datang.";

        return "🎪 Event Mendatang:\n\n" + string.Join("\n\n", events.Select(e =>
            $"   📛 {e.Title}\n" +
            $"   📅 {e.EventDate:dd MMM yyyy HH:mm}\n" +
            $"   📍 {e.Location}\n" +
            $"   👥 {e.CurrentParticipants}/{e.MaxParticipants} peserta\n" +
            (e.Fee.HasValue ? $"   💰 Fee: Rp {e.Fee.Value:N0}\n" : "") +
            $"   📝 {e.Summary}"));
    }

    // ---- DISCOUNTS ----

    [KernelFunction("get_active_discounts")]
    [Description("Mendapatkan daftar diskon dan promo yang sedang aktif")]
    public async Task<string> GetActiveDiscountsAsync()
    {
        var now = DateTime.UtcNow;
        var discounts = await _db.Discounts
            .Where(d => d.IsActive && d.ValidFrom <= now && d.ValidUntil >= now)
            .ToListAsync();

        if (!discounts.Any()) return "Tidak ada promo yang sedang aktif saat ini.";

        return "🏷️ Promo Aktif:\n\n" + string.Join("\n", discounts.Select(d =>
            $"   🔖 Kode: {d.Code}\n" +
            $"   💰 {d.Type}: {(d.Type == DiscountType.Percentage ? $"{d.Value}%" : $"Rp {d.Value:N0}")}\n" +
            $"   📝 {d.Description}\n" +
            $"   📅 Berlaku s/d {d.ValidUntil:dd MMM yyyy}\n" +
            $"   👥 Terpakai: {d.CurrentUses}/{(d.MaxUses?.ToString() ?? "∞")}"));
    }

    // ---- LEADERBOARD ----

    [KernelFunction("get_leaderboard")]
    [Description("Mendapatkan top 10 leaderboard member dengan poin tertinggi")]
    public async Task<string> GetLeaderboardAsync()
    {
        // Materialize dulu → assign rank di memory (.Select index-based tidak didukung SQLite)
        var topUsers = await _db.Users
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.LoyaltyPoints)
            .Take(10)
            .Select(u => new { u.FullName, u.LoyaltyPoints })
            .ToListAsync();

        if (!topUsers.Any()) return "Belum ada data leaderboard.";

        return "🏆 Top 10 Leaderboard:\n\n" + string.Join("\n", topUsers.Select((t, i) =>
            $"   {(i + 1 == 1 ? "🥇" : i + 1 == 2 ? "🥈" : i + 1 == 3 ? "🥉" : $"{i + 1}.")} {t.FullName} — ⭐ {t.LoyaltyPoints} pts"));
    }
}

/// <summary>Extension helper untuk truncate string</summary>
public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
