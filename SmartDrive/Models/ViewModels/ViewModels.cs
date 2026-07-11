using System.ComponentModel.DataAnnotations;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.ViewModels;

/// <summary>
/// ViewModel untuk registrasi user
/// </summary>
public class RegisterViewModel
{
    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public UserRole Role { get; set; } = UserRole.Student;

    public Gender? Gender { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// ViewModel untuk login
/// </summary>
public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

/// <summary>
/// ViewModel untuk booking jadwal
/// </summary>
public class BookingViewModel
{
    [Required]
    public int InstructorId { get; set; }

    public int? VehicleId { get; set; }

    public int? LocationId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// ViewModel untuk dashboard statistik
/// </summary>
public class DashboardStats
{
    public int TotalStudents { get; set; }
    public int TotalInstructors { get; set; }
    public int TotalVehicles { get; set; }
    public int ActiveBookings { get; set; }
    public int CompletedSessions { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int TotalBookingsThisMonth { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<string, int> BookingsByDay { get; set; } = new();
    public Dictionary<string, decimal> RevenueByMonth { get; set; } = new();
}

/// <summary>
/// ViewModel untuk profil user
/// </summary>
public class UserProfileViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; }
    public string? ProfilePicturePath { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? IdCardNumber { get; set; }
    public string? SimNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// ViewModel untuk pagination
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
}

/// <summary>
/// ViewModel untuk filter dan sorting
/// </summary>
public class DataTableRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortColumn { get; set; }
    public bool SortAscending { get; set; } = true;
    public string? SearchQuery { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
}

/// <summary>
/// ViewModel untuk chat bot
/// </summary>
public class ChatBotConfig
{
    public string ModelProvider { get; set; } = "OpenAI";
    public string ModelId { get; set; } = "gpt-4";
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = "Kamu adalah Om Bambang, asisten virtual yang ramah...";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public int TopP { get; set; } = 1;
}
