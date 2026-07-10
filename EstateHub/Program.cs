using MudBlazor;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Authorization;
using EstateHub.Data;
using EstateHub.Services;
using EstateHub.Services.Storage;
using EstateHub.Services.ChatBot;
using EstateHub.Components;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. Configure Services
// ============================================

// MudBlazor
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = true;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
});

// Database
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SQLite";
var connectionString = dbProvider switch
{
    "SqlServer" => builder.Configuration.GetConnectionString("SqlServer"),
    "MySql" => builder.Configuration.GetConnectionString("MySql"),
    "PostgreSql" => builder.Configuration.GetConnectionString("PostgreSql"),
    _ => builder.Configuration.GetConnectionString("DefaultConnection")
};

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer": options.UseSqlServer(connectionString); break;
        case "MySql": options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); break;
        case "PostgreSql": options.UseNpgsql(connectionString); break;
        default: options.UseSqlite(connectionString); break;
    }
});

// HTTP + HttpContext
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Storage Providers
builder.Services.AddStorageProviders();

// Application Services
builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<AdvertisingService>();
builder.Services.AddScoped<KprSimulatorService>();
builder.Services.AddScoped<ExportService>();

// Auth Services — in-memory only (no cookie, no HTTP header manipulation)
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EstateHubAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<EstateHubAuthStateProvider>());

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// ChatBot - Semantic Kernel with Kernel Functions
builder.Services.AddScoped<EstateHubKernelFunctions>();
builder.Services.AddScoped<ChatBotService>();

// ML Services
builder.Services.AddScoped<MLRecommendationService>();
builder.Services.AddScoped<PricePredictionService>();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SignalR
builder.Services.AddSignalR();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EstateHub API", Version = "v1" });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ============================================
// 2. Database Init
// ============================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ============================================
// 3. Middleware
// ============================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EstateHub API v1");
        c.RoutePrefix = "api/docs";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAntiforgery();

// ============================================
// 4. Endpoints
// ============================================

// Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Properties API
app.MapGet("/api/properties", async (AppDbContext db, int page = 1, int pageSize = 20) =>
{
    var total = await db.Properties.CountAsync();
    var items = await db.Properties.OrderByDescending(p => p.CreatedAt)
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    return Results.Ok(new { total, page, pageSize, data = items });
}).WithTags("Properties");

app.MapGet("/api/properties/{id:int}", async (AppDbContext db, int id) =>
{
    var p = await db.Properties.FindAsync(id);
    return p is not null ? Results.Ok(p) : Results.NotFound();
}).WithTags("Properties");

app.MapPost("/api/properties", async (AppDbContext db, EstateHub.Models.Property property) =>
{
    property.CreatedAt = DateTime.UtcNow;
    db.Properties.Add(property);
    await db.SaveChangesAsync();
    return Results.Created($"/api/properties/{property.Id}", property);
}).WithTags("Properties");

app.MapPut("/api/properties/{id:int}", async (AppDbContext db, int id, EstateHub.Models.Property updated) =>
{
    var p = await db.Properties.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Entry(p).CurrentValues.SetValues(updated);
    await db.SaveChangesAsync();
    return Results.Ok(p);
}).WithTags("Properties");

app.MapDelete("/api/properties/{id:int}", async (AppDbContext db, int id) =>
{
    var p = await db.Properties.FindAsync(id);
    if (p is null) return Results.NotFound();
    db.Properties.Remove(p);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("Properties");

app.MapGet("/api/users", async (AppDbContext db) => Results.Ok(await db.Users.ToListAsync())).WithTags("Users");
app.MapGet("/api/bookings", async (AppDbContext db) => Results.Ok(await db.Bookings.Include(b => b.Property).ToListAsync())).WithTags("Bookings");
app.MapGet("/api/payments", async (AppDbContext db) => Results.Ok(await db.Payments.ToListAsync())).WithTags("Payments");
app.MapGet("/api/contracts", async (AppDbContext db) => Results.Ok(await db.Contracts.ToListAsync())).WithTags("Contracts");

app.MapGet("/api/export/properties/csv", async (AppDbContext db, ExportService es) =>
{
    var props = await db.Properties.ToListAsync();
    var csv = es.ExportToCsv(props);
    return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "properties.csv");
}).WithTags("Export");

app.MapGet("/api/export/properties/excel", async (AppDbContext db, ExportService es) =>
{
    var props = await db.Properties.ToListAsync();
    var excel = es.ExportToExcel(props);
    return Results.File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "properties.xlsx");
}).WithTags("Export");

app.MapPost("/api/kpr/calculate", (KprSimulatorService svc, decimal price, decimal downPayment, double interestRate, int tenorMonths) =>
{
    var result = svc.Calculate(price, downPayment, interestRate, tenorMonths);
    return Results.Ok(result);
}).WithTags("KPR");

// ChatBot API
app.MapPost("/api/chatbot/send", async (ChatBotService bot, string message, int? sessionId) =>
{
    var response = await bot.SendMessageAsync(message, sessionId);
    return Results.Ok(response);
}).WithTags("ChatBot");

app.MapGet("/api/chatbot/sessions/{userId}", async (ChatBotService bot, string userId) =>
{
    var sessions = await bot.GetSessionsAsync(userId);
    return Results.Ok(sessions);
}).WithTags("ChatBot");

app.Run();
