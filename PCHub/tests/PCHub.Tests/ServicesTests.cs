using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Services;
using Xunit;

namespace PCHub.Tests;

/// <summary>Unit tests untuk Auth Service</summary>
public class AuthServiceTests
{
    private readonly AppDbContext _db;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("PCHub_Test_" + Guid.NewGuid())
            .Options;
        _db = new AppDbContext(options);
        _authService = new AuthService(_db);
    }

    [Fact]
    public async Task Register_NewUser_ReturnsAuthResponse()
    {
        var request = new RegisterRequest("testuser", "test@test.com", "Pass123!", "Test User", null);
        var result = await _authService.RegisterAsync(request);
        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("test@test.com", result.Email);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsNull()
    {
        await _authService.RegisterAsync(new RegisterRequest("dup", "a@a.com", "Pass123!", "A", null));
        var result = await _authService.RegisterAsync(new RegisterRequest("dup", "b@b.com", "Pass123!", "B", null));
        Assert.Null(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        await _authService.RegisterAsync(new RegisterRequest("loginuser", "login@test.com", "Pass123!", "Login User", null));
        var result = await _authService.LoginAsync(new LoginRequest("loginuser", "Pass123!"));
        Assert.NotNull(result);
        Assert.Equal("loginuser", result.Username);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsNull()
    {
        await _authService.RegisterAsync(new RegisterRequest("badpw", "bad@test.com", "Pass123!", "Bad PW", null));
        var result = await _authService.LoginAsync(new LoginRequest("badpw", "WrongPassword!"));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfile_ValidUser_ReturnsProfile()
    {
        var reg = await _authService.RegisterAsync(new RegisterRequest("prof", "prof@test.com", "Pass123!", "Profile User", "08123"));
        Assert.NotNull(reg);
        var profile = await _authService.GetProfileAsync(reg.UserId);
        Assert.NotNull(profile);
        Assert.Equal("08123", profile.PhoneNumber);
    }
}

/// <summary>Unit tests untuk Billing Service</summary>
public class BillingServiceTests
{
    private readonly AppDbContext _db;
    private readonly BillingService _billingService;

    public BillingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("PCHub_Test_Billing_" + Guid.NewGuid())
            .Options;
        _db = new AppDbContext(options);
        _billingService = new BillingService(_db);
    }

    [Fact]
    public async Task StartBilling_PcAvailable_CreatesSession()
    {
        var pc = new PCHub.Shared.Models.Pc { Id = Guid.NewGuid(), Name = "Test PC", PcNumber = "T01", Status = PcStatus.Available, HourlyRate = 8000 };
        var user = new PCHub.Shared.Models.User { Id = Guid.NewGuid(), Username = "test", Email = "t@t.com", PasswordHash = "x" };
        _db.Pcs.Add(pc);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _billingService.StartBillingAsync(new StartBillingRequest(user.Id, pc.Id, PaymentMethod.Cash));
        Assert.NotNull(result);
        Assert.Equal(BillingStatus.Active, result.Status);
        Assert.Equal(8000m, result.HourlyRate);
    }

    [Fact]
    public async Task StartBilling_PcNotAvailable_ThrowsException()
    {
        var pc = new PCHub.Shared.Models.Pc { Id = Guid.NewGuid(), Name = "Busy PC", PcNumber = "B01", Status = PcStatus.InUse, HourlyRate = 8000 };
        var user = new PCHub.Shared.Models.User { Id = Guid.NewGuid(), Username = "test2", Email = "t2@t.com", PasswordHash = "x" };
        _db.Pcs.Add(pc);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _billingService.StartBillingAsync(new StartBillingRequest(user.Id, pc.Id, PaymentMethod.Cash)));
    }
}

/// <summary>Unit tests untuk Helper functions</summary>
public class HelperTests
{
    [Theory]
    [InlineData(50000, "Rp 50.000")]
    [InlineData(0, "Rp 0")]
    [InlineData(1000000, "Rp 1.000.000")]
    public void FormatCurrency_Valid(decimal amount, string expected)
    {
        var result = PCHub.Shared.Utilities.Helpers.FormatCurrency(amount);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(30, "30 menit")]
    [InlineData(60, "1 jam")]
    [InlineData(90, "1 jam 30 menit")]
    [InlineData(120, "2 jam")]
    public void FormatDuration_Valid(int minutes, string expected)
    {
        var result = PCHub.Shared.Utilities.Helpers.FormatDuration(minutes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Hello World", 5, "He...")]
    [InlineData("Short", 10, "Short")]
    [InlineData("", 5, "")]
    public void Truncate_Valid(string input, int max, string expected)
    {
        var result = PCHub.Shared.Utilities.Helpers.Truncate(input, max);
        Assert.Equal(expected, result);
    }
}

/// <summary>Unit tests untuk ChatBot service</summary>
public class ChatBotServiceTests
{
    [Fact]
    public async Task SendMessage_CreatesResponse()
    {
        var service = new ChatBotService();
        var request = new SendChatMessageRequest(Guid.NewGuid(), "Halo, berapa harga sewa PC?", null, null);
        var result = await service.SendMessageAsync(request);
        Assert.NotNull(result);
        Assert.Equal("assistant", result.Role);
        Assert.Contains("Rp", result.Content);
    }

    [Fact]
    public async Task CreateSession_ReturnsSessionDto()
    {
        var service = new ChatBotService();
        var session = await service.CreateSessionAsync("Test Chat");
        Assert.NotNull(session);
        Assert.Equal("Test Chat", session.Title);
        Assert.Equal(0, session.MessageCount);
    }

    [Fact]
    public async Task MultipleMessages_IncrementCount()
    {
        var service = new ChatBotService();
        var session = await service.CreateSessionAsync();
        await service.SendMessageAsync(new SendChatMessageRequest(session.Id, "Hello", null, null));
        await service.SendMessageAsync(new SendChatMessageRequest(session.Id, "Harga?", null, null));

        var sessions = await service.GetSessionsAsync();
        var s = sessions.First(x => x.Id == session.Id);
        Assert.Equal(2, s.MessageCount);
    }
}
