using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ApexCharts;
using WashUp.Data;
using WashUp.Models;
using WashUp.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Database Configuration (SQLite default, supports PG & MSSQL)
// ============================================================
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=WashUp.db";

// Versi server MySQL ditentukan dari config (hindari AutoDetect yang membuka
// koneksi setiap kali DbContext dikonfigurasi)
var mySqlVersion = new MySqlServerVersion(
    Version.Parse(builder.Configuration["MySqlServerVersion"] ?? "8.0.36"));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "PostgreSQL":
            options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"));
            break;
        case "SqlServer":
            options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
            break;
        case "MySQL":
            options.UseMySql(builder.Configuration.GetConnectionString("MySQL"), mySqlVersion);
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
});

// ============================================================
// 2. Identity & Authentication (Cookie untuk web, JWT untuk API)
// ============================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

var jwtKey = builder.Configuration["Jwt:Key"] ?? "washup-dev-signing-key-change-in-production-1234567890";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "WashUp";
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// API endpoints menerima cookie (Swagger via browser login) ATAU bearer token
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Api", policy => policy
        .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
});

// ============================================================
// 3. Blazor & Razor Services
// ============================================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddApexCharts();

// ============================================================
// 4. Swagger / OpenAPI
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WashUp API",
        Version = "v1",
        Description = "REST API untuk aplikasi manajemen laundry WashUp. Login via POST /api/auth/token, lalu gunakan bearer token.",
        Contact = new OpenApiContact { Name = "WashUp Team", Email = "api@washup.id" }
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Masukkan token JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

// ============================================================
// 5. Application Services (DI)
// ============================================================
builder.Services.AddSingleton<IoTSimulatorService>();
builder.Services.AddSingleton<GpsSimulatorService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<ChatBotService>();

var app = builder.Build();

// ============================================================
// 6. Database Migration & Seed
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    try { await DataSeeder.SeedAsync(scope.ServiceProvider); }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Seeding skipped/failed");
    }
}

// ============================================================
// 7. Middleware Pipeline
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ============================================================
// 8. Minimal API Endpoints
// ============================================================
app.MapSwagger();

// --- Auth: tukar kredensial dengan JWT untuk integrasi eksternal ---
app.MapPost("/api/auth/token", async (UserManager<ApplicationUser> userManager, TokenRequest request) =>
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
        return Results.Unauthorized();

    var roles = await userManager.GetRolesAsync(user);
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Name, user.UserName ?? ""),
        new("FullName", user.FullName)
    };
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(jwtIssuer, null, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: creds);
    return Results.Ok(new
    {
        access_token = new JwtSecurityTokenHandler().WriteToken(token),
        token_type = "Bearer",
        expires_in = 8 * 3600
    });
}).WithTags("Auth");

// --- Logout via request HTTP biasa (cookie tidak bisa diubah dari circuit Blazor) ---
app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/auth/login");
}).ExcludeFromDescription();

// --- Health check ---
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, app = "WashUp" }))
    .WithTags("System");

// --- Orders API ---
app.MapGet("/api/orders", async (AppDbContext db, int page = 1, int pageSize = 20, string? status = null) =>
{
    var query = db.Orders.AsNoTracking().Include(o => o.User).AsQueryable();
    if (!string.IsNullOrEmpty(status)) query = query.Where(o => o.Status == status);

    var orders = await query
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(o => new
        {
            o.Id, o.OrderNumber, CustomerName = o.User != null ? o.User.FullName : "",
            o.ServiceType, o.WeightKg, o.TotalAmount, o.Status, o.PaymentStatus, o.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(orders);
}).WithTags("Orders").RequireAuthorization("Api");

app.MapGet("/api/orders/{orderNumber}", async (AppDbContext db, string orderNumber) =>
{
    var order = await db.Orders.AsNoTracking()
        .Where(o => o.OrderNumber == orderNumber)
        .Select(o => new
        {
            o.Id, o.OrderNumber,
            Customer = o.User != null ? o.User.FullName : "",
            o.ServiceType, o.WeightKg, o.ItemCount, o.ItemDescription,
            o.PricePerKg, o.Subtotal, o.Discount, o.TaxAmount, o.TotalAmount,
            o.Status, o.PaymentStatus, o.PaymentMethod,
            o.ReceivedAt, o.EstimatedCompletion, o.CompletedAt, o.DeliveredAt, o.CreatedAt,
            StatusLogs = o.StatusLogs.OrderBy(l => l.ChangedAt)
                .Select(l => new { l.OldStatus, l.NewStatus, l.ChangedAt }).ToList()
        })
        .FirstOrDefaultAsync();
    return order != null ? Results.Ok(order) : Results.NotFound(new { error = "Order not found" });
}).WithTags("Orders");

// --- Customers API ---
app.MapGet("/api/customers", async (AppDbContext db, int page = 1, int pageSize = 20) =>
{
    var customerRoleUserIds = db.UserRoles
        .Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Pelanggan"))
        .Select(ur => ur.UserId);

    var customers = await db.Users.AsNoTracking()
        .Where(u => customerRoleUserIds.Contains(u.Id))
        .OrderByDescending(u => u.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.MembershipTier, u.LoyaltyPoints, u.CreatedAt })
        .ToListAsync();
    return Results.Ok(customers);
}).WithTags("Customers").RequireAuthorization("Api");

// --- Branches API ---
app.MapGet("/api/branches", async (AppDbContext db) =>
    await db.Branches.AsNoTracking()
        .Where(b => b.IsActive)
        .Select(b => new { b.Id, b.Name, b.Address, b.Phone, b.Email, b.Latitude, b.Longitude })
        .ToListAsync())
    .WithTags("Branches");

// --- Marketplace API (public) ---
app.MapGet("/api/marketplace", async (AppDbContext db) =>
    await db.MarketplaceListings.AsNoTracking()
        .Where(m => m.IsActive)
        .Select(m => new { m.Id, m.BranchId, m.Title, m.Description, m.PricePerKg, m.ServiceArea, m.IsFeatured, m.AverageRating, m.ReviewCount })
        .ToListAsync())
    .WithTags("Marketplace");

// --- Dashboard summary API ---
app.MapGet("/api/dashboard/summary", async (AppDbContext db) =>
{
    var today = DateTime.UtcNow.Date;
    var thisMonth = new DateTime(today.Year, today.Month, 1);
    var totalOrders = await db.Orders.CountAsync();
    var activeOrders = await db.Orders.CountAsync(o => o.Status != "Selesai" && o.Status != "Dikirim");
    var monthRevenue = await db.FinancialTransactions
        .Where(t => t.TransactionType == "Income" && t.TransactionDate >= thisMonth)
        .SumAsync(t => t.Amount);
    var monthExpense = await db.FinancialTransactions
        .Where(t => t.TransactionType == "Expense" && t.TransactionDate >= thisMonth)
        .SumAsync(t => t.Amount);
    var totalCustomers = await db.Users.CountAsync();

    return Results.Ok(new
    {
        TotalOrders = totalOrders,
        ActiveOrders = activeOrders,
        MonthRevenue = monthRevenue,
        MonthExpense = monthExpense,
        MonthProfit = monthRevenue - monthExpense,
        TotalCustomers = totalCustomers,
        Today = today
    });
}).WithTags("Dashboard").RequireAuthorization("Api");

// --- Export laporan (CSV, kompatibel Excel) ---
app.MapGet("/api/reports/export/orders", async (AppDbContext db, DateTime? from, DateTime? to) =>
{
    var start = from ?? DateTime.UtcNow.AddMonths(-1);
    var end = to ?? DateTime.UtcNow;
    var orders = await db.Orders.AsNoTracking()
        .Include(o => o.User)
        .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
        .OrderBy(o => o.CreatedAt)
        .ToListAsync();

    var sb = new StringBuilder();
    sb.AppendLine("OrderNumber;Tanggal;Pelanggan;Layanan;BeratKg;Subtotal;Diskon;Pajak;Total;Status;StatusBayar;MetodeBayar");
    foreach (var o in orders)
    {
        sb.AppendLine(string.Join(';',
            Csv(o.OrderNumber), o.CreatedAt.ToString("yyyy-MM-dd HH:mm"), Csv(o.User?.FullName),
            Csv(o.ServiceType), o.WeightKg.ToString(System.Globalization.CultureInfo.InvariantCulture),
            o.Subtotal, o.Discount, o.TaxAmount, o.TotalAmount, Csv(o.Status), Csv(o.PaymentStatus), Csv(o.PaymentMethod)));
    }
    return CsvFile(sb, $"washup-orders-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
}).WithTags("Reports").RequireAuthorization("Api");

app.MapGet("/api/reports/export/finance", async (AppDbContext db, DateTime? from, DateTime? to) =>
{
    var start = from ?? DateTime.UtcNow.AddMonths(-6);
    var end = to ?? DateTime.UtcNow;
    var trans = await db.FinancialTransactions.AsNoTracking()
        .Where(t => t.TransactionDate >= start && t.TransactionDate <= end)
        .OrderBy(t => t.TransactionDate)
        .ToListAsync();

    var sb = new StringBuilder();
    sb.AppendLine("Tanggal;Tipe;Kategori;Deskripsi;Jumlah;CabangId");
    foreach (var t in trans)
        sb.AppendLine(string.Join(';', t.TransactionDate.ToString("yyyy-MM-dd"), Csv(t.TransactionType), Csv(t.Category), Csv(t.Description), t.Amount, t.BranchId));
    return CsvFile(sb, $"washup-finance-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
}).WithTags("Reports").RequireAuthorization("Api");

static string Csv(string? value) =>
    string.IsNullOrEmpty(value) ? "" : value.Contains(';') || value.Contains('"') || value.Contains('\n')
        ? "\"" + value.Replace("\"", "\"\"") + "\""
        : value;

static IResult CsvFile(StringBuilder sb, string fileName)
{
    // BOM agar Excel membaca UTF-8 dengan benar
    var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    return Results.File(bytes, "text/csv", fileName);
}

// ============================================================
// 9. Map Blazor & Run
// ============================================================
app.MapRazorComponents<WashUp.Components.App>()
    .AddInteractiveServerRenderMode();

// Start IoT and GPS simulators on background threads
var iotSimulator = app.Services.GetRequiredService<IoTSimulatorService>();
var gpsSimulator = app.Services.GetRequiredService<GpsSimulatorService>();
_ = Task.Run(() => iotSimulator.StartAsync());
_ = Task.Run(() => gpsSimulator.StartAsync());

app.Run();

record TokenRequest(string Email, string Password);
