using Comblang.Components;
using Comblang.Data;
using Comblang.Data.Seed;
using Comblang.Services.AI;
using Comblang.Services.Analytics;
using Comblang.Services.Auth;
using Comblang.Services.Chat;
using Comblang.Services.Matching;
using Comblang.Services.Storage;
using Comblang.Hubs;
using Comblang.Middleware;
using Comblang.API.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════
// 1. DATABASE
// ═══════════════════════════════════════════
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer": options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")); break;
        case "MySql": options.UseMySql(builder.Configuration.GetConnectionString("MySql"), ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySql"))); break;
        case "PostgreSql": options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql")); break;
        default: options.UseSqlite(builder.Configuration.GetConnectionString("SQLite")); break;
    }
});

// ═══════════════════════════════════════════
// 2. STORAGE
// ═══════════════════════════════════════════
builder.Services.AddSingleton<IStorageProvider>(_ =>
    StorageProviderFactory.Create(builder.Configuration, builder.Environment.ContentRootPath));

// ═══════════════════════════════════════════
// 3. AUTHENTICATION — Cookie + JWT
// ═══════════════════════════════════════════
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ═══════════════════════════════════════════
// 4. CORE SERVICES
// ═══════════════════════════════════════════
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MatchEngine>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ChatSessionService>();
builder.Services.AddScoped<SignalRConnectionManager>();          // SignalR connection tracking
builder.Services.AddScoped<TrafficService>();
builder.Services.AddScoped<KernelService>();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

// ═══════════════════════════════════════════
// 5. BLAZOR + SWAGGER
// ═══════════════════════════════════════════
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Comblang API", Version = "v1", Description = "REST API untuk aplikasi pencarian jodoh Comblang." });
});

// ═══════════════════════════════════════════
// 6. BUILD & PIPELINE
// ═══════════════════════════════════════════
var app = builder.Build();

// Auto-migrate + seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Comblang API v1"); options.RoutePrefix = "api/docs"; });
}
else { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); }

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.UseAuditLogging();
app.MapStaticAssets();

app.MapAuthEndpoints();
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notification");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
