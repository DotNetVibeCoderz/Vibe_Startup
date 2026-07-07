using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;
using FitnessCenter.Services;
using FitnessCenter.Services.Storage;
using FitnessCenter.Api;

var builder = WebApplication.CreateBuilder(args);

// ==================== DATABASE CONFIGURATION ====================
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SQLite";
var connectionString = builder.Configuration.GetValue<string>($"Database:ConnectionStrings:{dbProvider}")
    ?? "Data Source=fitness_center.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SQLServer":
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly("FitnessCenter"));
            break;
        case "MySQL":
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mySqlOptions =>
                mySqlOptions.MigrationsAssembly("FitnessCenter"));
            break;
        case "PostgreSQL":
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly("FitnessCenter"));
            break;
        case "SQLite":
        default:
            options.UseSqlite(connectionString, sqliteOptions =>
                sqliteOptions.MigrationsAssembly("FitnessCenter"));
            break;
    }
});

// ==================== STORAGE CONFIGURATION ====================
var storageProvider = builder.Configuration.GetValue<string>("Storage:Provider") ?? "FileSystem";
builder.Services.AddScoped<IStorageProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();

    try
    {
        return storageProvider.ToLowerInvariant() switch
        {
            "azureblob" or "azure" => ActivatorUtilities.CreateInstance<AzureBlobStorageProvider>(sp),
            "s3" or "aws" => ActivatorUtilities.CreateInstance<S3StorageProvider>(sp),
            "minio" => ActivatorUtilities.CreateInstance<MinIOStorageProvider>(sp),
            _ => ActivatorUtilities.CreateInstance<FileSystemStorageProvider>(sp)
        };
    }
    catch (Exception ex)
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to initialize '{Provider}' storage provider, falling back to FileSystem", storageProvider);
        return ActivatorUtilities.CreateInstance<FileSystemStorageProvider>(sp);
    }
});

// ==================== IDENTITY ====================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;
});

// ==================== SERVICES ====================
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<MembershipService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<ClassService>();
builder.Services.AddScoped<TrainerService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<WorkoutService>();
builder.Services.AddScoped<NutritionService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<GamificationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DiscountService>();
builder.Services.AddScoped<ChatBotService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<DataSeedService>();

// ==================== BLAZOR + SIGNALR (LARGE FILE UPLOAD) ====================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Perbesar batas ukuran upload file via Blazor Server SignalR circuit
        // Default 32KB → naikkan ke 10MB untuk foto profil & upload
        options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(5);
    });

// SignalR hub config — biar InputFile bisa upload sampai 10MB
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    options.EnableDetailedErrors = true;
});

// ==================== SWAGGER ====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ==================== DATABASE MIGRATION & SEED ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database initialization warning");
    }

    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeedService>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Data seeding warning");
    }
}

// ==================== MIDDLEWARE PIPELINE ====================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "FitnessCenter API v1");
    options.RoutePrefix = "api/docs";
});

// ==================== AUTH ENDPOINTS ====================
app.MapPost("/login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    [FromForm] string email,
    [FromForm] string password,
    [FromForm] string? returnUrl) =>
{
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect($"/login?error=Email%20dan%20password%20wajib&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        return Results.Redirect($"/login?error=Invalid%20email%20or%20password&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    var result = await signInManager.PasswordSignInAsync(user, password, true, false);
    if (!result.Succeeded)
    {
        return Results.Redirect($"/login?error=Invalid%20email%20or%20password&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
        ? "/"
        : returnUrl;

    return Results.LocalRedirect(safeReturnUrl);
});

app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

// Map Minimal API endpoints
ApiEndpoints.MapApiEndpoints(app);

// Map Blazor
app.MapRazorComponents<FitnessCenter.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
