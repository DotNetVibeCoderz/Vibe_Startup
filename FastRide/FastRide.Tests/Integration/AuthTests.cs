using System.Net;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Register_IssuesATokenForANewRider()
    {
        var client = fixture.NewClient();
        var email = fixture.NextEmail("new.rider");

        var auth = await client.PostAndReadAsync<RegisterRequest, AuthResponse>(
            "/api/auth/register",
            new RegisterRequest("Rider Baru", email, "08120001111", ApiFixture.Password, UserRole.Rider));

        Assert.NotEqual(Guid.Empty, auth.UserId);
        Assert.Equal(UserRole.Rider, auth.Role);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.True(auth.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_GivesANewAccountAGeneratedAvatar()
    {
        var rider = await fixture.NewRiderAsync();
        var profile = await rider.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{rider.Id}");

        Assert.StartsWith("data:image/svg+xml;base64,", profile.PhotoUrl);
    }

    [Fact]
    public async Task Register_RefusesToCreateAnAdmin()
    {
        var client = fixture.NewClient();

        using var response = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest("Penyusup", fixture.NextEmail("admin.attempt"), "0812", ApiFixture.Password, UserRole.Admin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsAnEmailThatIsAlreadyTaken()
    {
        var rider = await fixture.NewRiderAsync();
        var client = fixture.NewClient();

        using var response = await client.PostJsonAsync("/api/auth/register",
            new RegisterRequest("Kembar", rider.Email, "0812", ApiFixture.Password, UserRole.Rider));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsTheSameRefusal_ForAWrongPasswordAndAnUnknownEmail()
    {
        // Different answers would let anyone map which emails are registered.
        var rider = await fixture.NewRiderAsync();
        var client = fixture.NewClient();

        using var wrongPassword = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(rider.Email, "SalahSekali123"));

        using var unknownEmail = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest("tidak.ada@fastride.test", "SalahSekali123"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            await wrongPassword.Content.ReadAsStringAsync(),
            await unknownEmail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Me_ReturnsTheSignedInUser()
    {
        var rider = await fixture.NewRiderAsync();

        var profile = await rider.Client.GetAndReadAsync<UserProfileResponse>("/api/auth/me");

        Assert.Equal(rider.Id, profile.Id);
        Assert.Equal(rider.Email, profile.Email);
        Assert.Equal(UserRole.Rider, profile.Role);
    }

    [Fact]
    public async Task Logout_InvalidatesTheTokenImmediately()
    {
        // A JWT cannot be recalled, so logout has to bump the security stamp.
        var rider = await fixture.NewRiderAsync();

        using var beforeLogout = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, beforeLogout.StatusCode);

        using var logout = await rider.Client.PostAsync("/api/auth/logout", null);
        logout.EnsureSuccessStatusCode();

        using var afterLogout = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_EndsEveryExistingSession()
    {
        var rider = await fixture.NewRiderAsync();

        using var change = await rider.Client.PostJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest(ApiFixture.Password, "PasswordBaru123"));

        change.EnsureSuccessStatusCode();

        // The token that performed the change is itself no longer valid.
        using var reuse = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        using var freshClient = await fixture.SignInAsync(rider.Email, "PasswordBaru123");
        using var afterSignIn = await freshClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, afterSignIn.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RefusesAWrongCurrentPassword()
    {
        var rider = await fixture.NewRiderAsync();

        using var response = await rider.Client.PostJsonAsync("/api/auth/change-password",
            new ChangePasswordRequest("BukanIni123", "PasswordBaru123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AnswersIdentically_ForKnownAndUnknownEmails()
    {
        var rider = await fixture.NewRiderAsync();
        var client = fixture.NewClient();

        var known = await client.PostAndReadAsync<ForgotPasswordRequest, ForgotPasswordResponse>(
            "/api/auth/forgot-password", new ForgotPasswordRequest(rider.Email));

        var unknown = await client.PostAndReadAsync<ForgotPasswordRequest, ForgotPasswordResponse>(
            "/api/auth/forgot-password", new ForgotPasswordRequest("tidak.ada@fastride.test"));

        Assert.Equal(known.Message, unknown.Message);

        // In Development the code comes back so the flow is testable; for an unknown address
        // there is nothing to send.
        Assert.NotNull(known.ResetCode);
        Assert.Null(unknown.ResetCode);
    }

    [Fact]
    public async Task ResetPassword_SwapsTheCredentialAndEndsOldSessions()
    {
        var rider = await fixture.NewRiderAsync();
        var client = fixture.NewClient();

        var request = await client.PostAndReadAsync<ForgotPasswordRequest, ForgotPasswordResponse>(
            "/api/auth/forgot-password", new ForgotPasswordRequest(rider.Email));

        using var reset = await client.PostJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rider.Email, request.ResetCode!, "PasswordSetelahReset1"));

        reset.EnsureSuccessStatusCode();

        using var oldToken = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);

        using var oldPassword = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(rider.Email, ApiFixture.Password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        using var newPassword = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(rider.Email, "PasswordSetelahReset1"));
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_RefusesAWrongCode()
    {
        var rider = await fixture.NewRiderAsync();
        var client = fixture.NewClient();

        await client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(rider.Email));

        using var response = await client.PostJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(rider.Email, "000000", "PasswordBaru123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SuspendedAccount_CannotSignIn()
    {
        var rider = await fixture.NewRiderAsync();

        using var suspend = await fixture.Admin.PutJsonAsync(
            $"/api/admin/users/{rider.Id}/active", new SetUserActiveRequest(false, "Uji"));

        suspend.EnsureSuccessStatusCode();

        var client = fixture.NewClient();
        using var login = await client.PostJsonAsync("/api/auth/login",
            new LoginRequest(rider.Email, ApiFixture.Password));

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task SuspendingAnAccount_CutsItsLiveSession()
    {
        var rider = await fixture.NewRiderAsync();

        using var before = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var suspend = await fixture.Admin.PutJsonAsync(
            $"/api/admin/users/{rider.Id}/active", new SetUserActiveRequest(false, "Uji"));
        suspend.EnsureSuccessStatusCode();

        using var after = await rider.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotSuspendItself()
    {
        var admin = await fixture.Admin.GetAndReadAsync<UserProfileResponse>("/api/auth/me");

        using var response = await fixture.Admin.PutJsonAsync(
            $"/api/admin/users/{admin.Id}/active", new SetUserActiveRequest(false, "Oops"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
