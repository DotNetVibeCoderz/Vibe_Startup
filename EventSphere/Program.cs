using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;
using EventSphere.Services;
using EventSphere.Api;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// 1. Database Configuration (Multi-Provider)
// ──────────────────────────────────────────────
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=EventSphere.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer":
            options.UseSqlServer(connectionString);
            break;
        case "MySQL":
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            break;
        case "PostgreSQL":
            options.UseNpgsql(connectionString);
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
});

// ──────────────────────────────────────────────
// 2. Identity & Authentication
// ──────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    // 🔑 Jangan set LogoutPath — halaman /logout menangani SignOutAsync sendiri
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// ──────────────────────────────────────────────
// 3. Authorization Policies
// ──────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("OrganizerAccess", p => p.RequireRole("Admin", "Organizer"));
    options.AddPolicy("ClientAccess", p => p.RequireRole("Admin", "Organizer", "Client"));
    options.AddPolicy("VendorAccess", p => p.RequireRole("Admin", "Organizer", "Vendor"));
    options.AddPolicy("AllUsers", p => p.RequireRole("Admin", "Organizer", "Client", "Vendor", "Guest", "Moderator"));
});

// ──────────────────────────────────────────────
// 4. Application Services
// ──────────────────────────────────────────────
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<VendorService>();
builder.Services.AddScoped<GuestService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<DashboardService>();

// ──────────────────────────────────────────────
// 5. Blazor + HTTP + OpenAPI
// ──────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Anthropic");
builder.Services.AddHttpClient("Scraper");

builder.Services.AddOpenApi("v1");

// ──────────────────────────────────────────────
// 6. Build App
// ──────────────────────────────────────────────
var app = builder.Build();

// ──────────────────────────────────────────────
// 7. Database Migration & Seed
// ──────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    var seeder = new DataSeeder(
        db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    await seeder.SeedAsync();
}

// ──────────────────────────────────────────────
// 8. Middleware Pipeline
// ──────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseMiddleware<ApiKeyMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ──────────────────────────────────────────────
// 9. Auth Endpoints (HTTP Only)
// ──────────────────────────────────────────────
app.MapPost("/auth/login", async (
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { error = "Email dan password wajib diisi." });

    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is null)
        return Results.Unauthorized();

    var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, false);

    if (result.Succeeded)
        return Results.Ok(new { success = true });

    if (result.IsLockedOut)
        return Results.Problem("Akun terkunci. Silakan coba lagi nanti.", statusCode: 423);

    if (result.RequiresTwoFactor)
        return Results.Problem("Verifikasi 2FA diperlukan.", statusCode: 412);

    return Results.Unauthorized();
})
.AllowAnonymous()
.DisableAntiforgery();

app.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Ok(new { success = true });
})
.DisableAntiforgery();

// ──────────────────────────────────────────────
// 10. REST API & Swagger
// ──────────────────────────────────────────────
app.MapApiEndpoints();
app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "EventSphere API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "EventSphere API Documentation";
});

// ──────────────────────────────────────────────
// 11. Blazor
// ──────────────────────────────────────────────
app.MapRazorComponents<EventSphere.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

internal record LoginRequest(string Email, string Password, bool RememberMe);
