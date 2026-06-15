using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SoccerWizard.Api;
using SoccerWizard.Components;
using SoccerWizard.Data;
using SoccerWizard.Hubs;
using SoccerWizard.Models;
using SoccerWizard.Services;
using SoccerWizard.Services.Data;
using SoccerWizard.Services.LLM;
using SoccerWizard.Services.Storage;
using SoccerWizard.Services.VectorData;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    DatabaseProviderFactory.Configure(builder.Configuration, options));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IStorageService>(sp => StorageServiceFactory.Create(sp));
builder.Services.AddSingleton<VectorDataService>();
builder.Services.AddSingleton<SemanticKernelService>();
builder.Services.AddSingleton<LLMService>();

builder.Services.AddSingleton<LiveDataSyncSettings>();
builder.Services.AddHostedService<LiveDataSyncHostedService>();

builder.Services.AddHttpClient<SemanticKernelService>();
builder.Services.AddHttpClient("FootballData", client =>
{
    var baseUrl = builder.Configuration["SportsData:BaseUrl"] ?? "https://api.football-data.org/v4";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");

    var apiKey = builder.Configuration["SportsData:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSignalR(o => { o.EnableDetailedErrors = true; });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SoccerWizard API", Version = "v2.0" });
    c.AddSecurityDefinition("ApiKey", new() { Name = "X-Api-Key", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey });
});
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<MLPredictionService>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    try { await DataSeeder.SeedAsync(scope.ServiceProvider); }
    catch (Exception ex) { Console.WriteLine($"Seed error: {ex.Message}"); }

// ==================== AUTH ENDPOINTS ====================
app.MapPost("/login", async (HttpContext http, SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users,
    [FromForm] string? email, [FromForm] string? password, [FromForm] bool? rememberMe) =>
{
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        http.Response.Redirect("/auth/login?error=Invalid+email+or+password");
        return;
    }

    var user = await users.FindByEmailAsync(email);
    if (user is null)
    {
        http.Response.Redirect("/auth/login?error=Invalid+email+or+password");
        return;
    }

    var result = await signIn.PasswordSignInAsync(user, password, rememberMe ?? false, false);
    http.Response.Redirect(result.Succeeded ? "/" : "/auth/login?error=Invalid+email+or+password");
}).DisableAntiforgery();

app.MapPost("/logout", async (HttpContext http, SignInManager<IdentityUser> signIn) =>
{
    await signIn.SignOutAsync();
    http.Response.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/register", async (HttpContext http, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, IDbContextFactory<AppDbContext> dbFactory,
    [FromForm] string? email, [FromForm] string? password, [FromForm] string? confirmPassword, [FromForm] string? fullName) =>
{
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword) || string.IsNullOrWhiteSpace(fullName))
    {
        http.Response.Redirect("/auth/register?error=Registration+failed");
        return;
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
        http.Response.Redirect("/auth/register?error=Password+confirmation+does+not+match");
        return;
    }

    await using var db = await dbFactory.CreateDbContextAsync();
    var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
    var result = await users.CreateAsync(user, password);
    if (!result.Succeeded) { http.Response.Redirect("/auth/register?error=Registration+failed"); return; }
    await users.AddToRoleAsync(user, "User");
    db.UserProfiles.Add(new UserProfile { UserId = user.Id, FullName = fullName, CreatedAt = DateTime.UtcNow, LastLoginAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    await signIn.SignInAsync(user, false);
    http.Response.Redirect("/");
}).DisableAntiforgery();

app.MapPost("/reset-password", async (HttpContext http, UserManager<IdentityUser> users,
    [FromForm] string? email, [FromForm] string? newPassword, [FromForm] string? confirmPassword) =>
{
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
    {
        http.Response.Redirect("/auth/reset-password?error=Please+complete+all+fields");
        return;
    }

    if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
    {
        http.Response.Redirect("/auth/reset-password?error=Password+confirmation+does+not+match");
        return;
    }

    var user = await users.FindByEmailAsync(email);
    if (user is null)
    {
        http.Response.Redirect("/auth/reset-password?error=Account+not+found");
        return;
    }

    var token = await users.GeneratePasswordResetTokenAsync(user);
    var resetResult = await users.ResetPasswordAsync(user, token, newPassword);
    if (!resetResult.Succeeded)
    {
        http.Response.Redirect("/auth/reset-password?error=Password+reset+failed");
        return;
    }

    http.Response.Redirect("/auth/login?msg=Password+updated.+Please+sign+in");
}).DisableAntiforgery();

app.MapGet("/admin/manual-sync/template", () =>
{
    var fileBytes = ManualSyncExcelTemplate.BuildTemplate();
    return Results.File(fileBytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ManualSyncExcelTemplate.TemplateFileName);
}).RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

// ==================== MIDDLEWARE ====================
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v2.0"); c.RoutePrefix = "swagger"; });
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseApiKeyAuth();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapApiEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapHub<MatchHub>("/matchhub");

Console.WriteLine($"SoccerWizard | DB:{dbProvider} | http://localhost:5024 | /swagger");
app.Run();
