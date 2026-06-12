using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastRide.Api.Infrastructure;
using FastRide.Data;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════ SERVICES ═══════════════════
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ── Multi-Database Provider ──
var dbProvider = builder.Configuration["Database:Provider"]?.ToLowerInvariant() ?? "sqlite";
builder.Services.AddDbContext<FastRideDbContext>(options =>
{
    var cs = dbProvider switch
    {
        "sqlserver" or "mssql" => builder.Configuration["Database:ConnectionStrings:SqlServer"],
        "postgresql" or "postgres" or "npgsql" => builder.Configuration["Database:ConnectionStrings:PostgreSQL"],
        "mysql" => builder.Configuration["Database:ConnectionStrings:MySQL"],
        _ => builder.Configuration["Database:ConnectionStrings:SQLite"]
    } ?? "Data Source=FastRide.db";

    switch (dbProvider)
    {
        case "sqlserver" or "mssql": options.UseSqlServer(cs); break;
        case "postgresql" or "postgres" or "npgsql": options.UseNpgsql(cs); break;
        case "mysql": options.UseMySql(cs, ServerVersion.AutoDetect(cs)); break;
        default: options.UseSqlite(cs); break;
    }
});

// ── Storage Provider ──
var storageProvider = StorageProviderFactory.Create(builder.Configuration);
builder.Services.AddSingleton(storageProvider);

// ── CORS ──
var corsOrigins = builder.Configuration.GetSection("ApiSettings:CorsOrigins").Get<string[]>()
                  ?? new[] { "https://localhost:5002" };
builder.Services.AddCors(o => o.AddPolicy("AllowClients", p =>
    p.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader()));

// ── JWT Auth ──
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "FastRide-Super-Secret-Key-Minimum-32-Characters-Long!";
builder.Services.AddAuthentication("Bearer").AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "FastRide",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "FastRide",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

// ── DB Init ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FastRideDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SampleDataSeeder.SeedAsync(db);
}

// ── Middleware ──
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseCors("AllowClients");
app.UseHttpsRedirection();
app.UseStaticFiles(); // Serve uploads folder as static files
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
var storage = storageProvider;

// ═══════════════ HEALTH ═══════════════
api.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "1.0.0", database = dbProvider, storageProvider = builder.Configuration["Storage:Provider"] ?? "FileSystem" }))
   .WithTags("Health").AllowAnonymous();

// ═══════════════ AUTH ═══════════════
api.MapPost("/auth/register", async (RegisterDto dto, FastRideDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Email == dto.Email)) return Results.Conflict(new { error = "Email already registered" });
    var user = new User { FullName = dto.FullName, Email = dto.Email, PhoneNumber = dto.PhoneNumber, PasswordHash = BCryptHash(dto.Password), Role = dto.Role, PhotoUrl = GenerateAvatarUrl(dto.FullName) };
    db.Users.Add(user);
    if (dto.Role == UserRole.Driver)
        db.DriverProfiles.Add(new DriverProfile { UserId = user.Id, LicenseNumber = dto.LicenseNumber ?? "PENDING", VehicleType = dto.VehicleType ?? "Unknown", VehiclePlate = dto.VehiclePlate ?? "PENDING" });
    await db.SaveChangesAsync();
    var token = GenJwt(user, builder.Configuration);
    return Results.Created("/api/profile", new AuthResponse(user.Id, user.FullName, user.Email, token, user.Role, DateTime.UtcNow.AddDays(1), user.PhotoUrl, "image/svg+xml"));
}).WithTags("Auth").AllowAnonymous();

api.MapPost("/auth/login", async (LoginDto dto, FastRideDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
    if (user == null || !BCryptVerify(dto.Password, user.PasswordHash)) return Results.Unauthorized();
    return Results.Ok(new AuthResponse(user.Id, user.FullName, user.Email, GenJwt(user, builder.Configuration), user.Role, DateTime.UtcNow.AddDays(1), user.PhotoUrl, user.ProfilePhotoMimeType));
}).WithTags("Auth").AllowAnonymous();

// ═══════════════ PROFILE (with storage) ═══════════════
api.MapGet("/profile/{userId:guid}", async (Guid userId, FastRideDbContext db) =>
{
    var u = await db.Users.Where(x => x.Id == userId).Include(x => x.DriverProfile).Select(x => new {
        x.Id, x.FullName, x.Email, x.PhoneNumber, x.Role, x.IsVerified, x.CreatedAt, x.PhotoUrl, x.ProfilePhotoMimeType,
        Driver = x.DriverProfile != null ? new { x.DriverProfile.LicenseNumber, x.DriverProfile.VehicleType, x.DriverProfile.VehiclePlate, Status = x.DriverProfile.Status.ToString(), x.DriverProfile.Rating, x.DriverProfile.TotalTrips, x.DriverProfile.TotalEarnings } : null
    }).FirstOrDefaultAsync();
    return u is null ? Results.NotFound() : Results.Ok(u);
}).WithTags("Profile");

api.MapPut("/profile/{userId:guid}", async (Guid userId, HttpRequest req, FastRideDbContext db) =>
{
    var u = await db.Users.FindAsync(userId);
    if (u == null) return Results.NotFound();

    // Handle multipart form for photo upload
    if (req.HasFormContentType)
    {
        var form = await req.ReadFormAsync();
        if (!string.IsNullOrWhiteSpace(form["fullName"])) u.FullName = form["fullName"]!;
        if (!string.IsNullOrWhiteSpace(form["phoneNumber"])) u.PhoneNumber = form["phoneNumber"]!;

        var file = form.Files.GetFile("photo");
        if (file != null && file.Length > 0 && file.Length < 512 * 1024)
        {
            var ext = Path.GetExtension(file.FileName) ?? ".jpg";
            var fileName = storage.GeneratePhotoFileName(userId, ext);
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            u.PhotoUrl = await storage.UploadAsync(fileName, ms.ToArray(), file.ContentType);
            u.ProfilePhotoMimeType = file.ContentType;
        }
    }
    else
    {
        // JSON body for text-only updates
        try
        {
            var update = await req.ReadFromJsonAsync<UpdateProfileReq>();
            if (update != null)
            {
                if (!string.IsNullOrWhiteSpace(update.FullName)) u.FullName = update.FullName;
                if (!string.IsNullOrWhiteSpace(update.PhoneNumber)) u.PhoneNumber = update.PhoneNumber;
                // Base64 photo as fallback
                if (!string.IsNullOrWhiteSpace(update.ProfilePhotoBase64) && update.ProfilePhotoBase64.Length < 700_000)
                {
                    var data = Convert.FromBase64String(update.ProfilePhotoBase64);
                    var fileName = storage.GeneratePhotoFileName(userId, update.ProfilePhotoMimeType?.Contains("png") == true ? "png" : "jpg");
                    u.PhotoUrl = await storage.UploadAsync(fileName, data, update.ProfilePhotoMimeType ?? "image/jpeg");
                    u.ProfilePhotoMimeType = update.ProfilePhotoMimeType ?? "image/jpeg";
                }
            }
        }
        catch { /* ignore parse error */ }
    }

    u.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { u.Id, u.FullName, u.PhoneNumber, u.PhotoUrl, u.ProfilePhotoMimeType, u.UpdatedAt });
}).WithTags("Profile");

api.MapDelete("/profile/{userId:guid}/photo", async (Guid userId, FastRideDbContext db) =>
{
    var u = await db.Users.FindAsync(userId);
    if (u == null) return Results.NotFound();
    if (u.PhotoUrl != null)
    {
        var fileName = Path.GetFileName(new Uri(u.PhotoUrl).AbsolutePath);
        await storage.DeleteAsync(fileName);
    }
    u.PhotoUrl = null; u.ProfilePhotoMimeType = null; u.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { message = "Photo removed" });
}).WithTags("Profile");

// ═══════════════ RIDERS / DRIVERS / ORDERS / PAYMENTS / PROMOS / REVIEWS / DASHBOARD / MOBILE ═══════════════
api.MapGet("/riders", async (int? page, int? limit, string? search, FastRideDbContext db) => { var q = db.Users.Where(u => u.Role == UserRole.Rider).AsQueryable(); if (!string.IsNullOrWhiteSpace(search)) { var t = search.ToLower(); q = q.Where(u => u.FullName.ToLower().Contains(t) || u.Email.ToLower().Contains(t)); } var total = await q.CountAsync(); var data = await q.OrderByDescending(u => u.CreatedAt).Skip(((page ?? 1) - 1) * (limit ?? 20)).Take(limit ?? 20).Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.IsVerified, u.CreatedAt, u.PhotoUrl, TotalTrips = u.RiderOrders.Count }).ToListAsync(); return Results.Ok(new { total, page = page ?? 1, limit = limit ?? 20, data }); }).WithTags("Riders");

api.MapGet("/drivers", async (int? page, int? limit, string? search, FastRideDbContext db) => { var q = db.Users.Where(u => u.Role == UserRole.Driver).Include(u => u.DriverProfile).AsQueryable(); if (!string.IsNullOrWhiteSpace(search)) { var t = search.ToLower(); q = q.Where(u => u.FullName.ToLower().Contains(t) || u.Email.ToLower().Contains(t)); } var total = await q.CountAsync(); var data = await q.OrderByDescending(u => u.DriverProfile!.Rating).Skip(((page ?? 1) - 1) * (limit ?? 20)).Take(limit ?? 20).Select(u => new { u.Id, u.FullName, u.Email, u.PhotoUrl, Status = u.DriverProfile!.Status.ToString(), u.DriverProfile.Rating, u.DriverProfile.TotalTrips, u.DriverProfile.TotalEarnings, u.DriverProfile.VehicleType, u.DriverProfile.VehiclePlate, u.DriverProfile.CurrentLatitude, u.DriverProfile.CurrentLongitude }).ToListAsync(); return Results.Ok(new { total, page = page ?? 1, limit = limit ?? 20, data }); }).WithTags("Drivers");

api.MapGet("/orders", async (string? status, int? page, int? limit, FastRideDbContext db) => { var q = db.Orders.Include(o => o.Rider).Include(o => o.Driver).AsQueryable(); if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var os)) q = q.Where(o => o.Status == os); var total = await q.CountAsync(); var data = await q.OrderByDescending(o => o.CreatedAt).Skip(((page ?? 1) - 1) * (limit ?? 25)).Take(limit ?? 25).Select(o => new { o.Id, RiderId = o.RiderId, RiderName = o.Rider.FullName, DriverId = o.DriverId, DriverName = o.Driver != null ? o.Driver.FullName : null, o.PickupAddress, o.DropoffAddress, o.DistanceKm, o.EstimatedDurationMinutes, o.EstimatedFare, o.FinalFare, VehicleCategory = o.VehicleCategory.ToString(), PaymentMethod = o.PaymentMethod.ToString(), Status = o.Status.ToString(), o.CreatedAt, o.CompletedAt }).ToListAsync(); return Results.Ok(new { total, page = page ?? 1, limit = limit ?? 25, data }); }).WithTags("Orders");

api.MapPost("/orders", async (CreateOrderReq req, FastRideDbContext db) => { if (!await db.Users.AnyAsync(u => u.Id == req.RiderId && u.Role == UserRole.Rider)) return Results.BadRequest(new { error = "Rider not found" }); var fc = await db.FareConfigs.FirstOrDefaultAsync(x => x.VehicleCategory == req.VehicleCategory); var dist = Haversine(req.PickupLatitude, req.PickupLongitude, req.DropoffLatitude, req.DropoffLongitude); var est = fc != null ? fc.BaseFare + fc.CostPerKm * (decimal)dist + fc.CostPerMinute * 10 : 15000m; decimal final = est; string? promoApplied = null; if (!string.IsNullOrWhiteSpace(req.PromoCode)) { var promo = await db.Promos.FirstOrDefaultAsync(p => p.Code == req.PromoCode.ToUpper() && p.IsActive && p.ValidFrom <= DateTime.UtcNow && p.ValidUntil >= DateTime.UtcNow); if (promo != null && promo.UsageCount < promo.UsageLimit) { final = promo.Type == PromoType.Percentage ? est - Math.Min(est * promo.Value / 100, promo.MaxDiscount > 0 ? promo.MaxDiscount : decimal.MaxValue) : est - promo.Value; if (final < 0) final = 0; promo.UsageCount++; promoApplied = promo.Code; } } var order = new Order { RiderId = req.RiderId, PickupLatitude = req.PickupLatitude, PickupLongitude = req.PickupLongitude, PickupAddress = req.PickupAddress, DropoffLatitude = req.DropoffLatitude, DropoffLongitude = req.DropoffLongitude, DropoffAddress = req.DropoffAddress, DistanceKm = Math.Round(dist, 1), EstimatedDurationMinutes = (int)(dist * 2.5) + 5, EstimatedFare = est, FinalFare = final, VehicleCategory = req.VehicleCategory, PaymentMethod = req.PaymentMethod, Status = OrderStatus.Requested }; db.Orders.Add(order); await db.SaveChangesAsync(); return Results.Created($"/api/orders/{order.Id}", new { order.Id, Status = order.Status.ToString(), order.EstimatedFare, order.FinalFare, order.DistanceKm, PromoApplied = promoApplied, order.CreatedAt }); }).WithTags("Orders");

api.MapGet("/payments", async (int? page, int? limit, FastRideDbContext db) => { var total = await db.Payments.CountAsync(); var data = await db.Payments.OrderByDescending(p => p.CreatedAt).Skip(((page ?? 1) - 1) * (limit ?? 25)).Take(limit ?? 25).Select(p => new { p.Id, p.OrderId, p.Amount, Method = p.Method.ToString(), Status = p.Status.ToString(), p.CreatedAt, p.CompletedAt, p.TransactionReference }).ToListAsync(); return Results.Ok(new { total, page = page ?? 1, limit = limit ?? 25, data }); }).WithTags("Payments");
api.MapPost("/payments", async (PaymentRequest req, FastRideDbContext db) => { var order = await db.Orders.FindAsync(req.OrderId); if (order == null) return Results.NotFound(); var p = new Payment { OrderId = req.OrderId, Amount = req.Amount > 0 ? req.Amount : order.FinalFare, Method = req.Method, Status = PaymentStatus.Completed, CompletedAt = DateTime.UtcNow, TransactionReference = $"TRX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}" }; db.Payments.Add(p); order.Status = OrderStatus.Completed; order.CompletedAt = DateTime.UtcNow; order.FinalFare = p.Amount; await db.SaveChangesAsync(); return Results.Created($"/api/payments/{p.Id}", new { p.Id, p.OrderId, p.Amount, Method = p.Method.ToString(), Status = p.Status.ToString(), p.TransactionReference }); }).WithTags("Payments");

api.MapGet("/promos", async (FastRideDbContext db) => Results.Ok(await db.Promos.OrderByDescending(p => p.IsActive).ThenBy(p => p.Code).Select(p => new { p.Id, p.Code, p.Description, Type = p.Type.ToString(), p.Value, p.MaxDiscount, p.ValidFrom, p.ValidUntil, p.IsActive, p.UsageLimit, p.UsageCount }).ToListAsync())).WithTags("Promos");

api.MapPost("/promos/validate", async (ValidatePromoDto dto, FastRideDbContext db) => { var promo = await db.Promos.FirstOrDefaultAsync(p => p.Code == dto.Code.ToUpper() && p.IsActive && p.ValidFrom <= DateTime.UtcNow && p.ValidUntil >= DateTime.UtcNow); if (promo == null) return Results.NotFound(new { valid = false, message = "Promo not found" }); if (promo.UsageCount >= promo.UsageLimit) return Results.BadRequest(new { valid = false, message = "Limit reached" }); var discount = promo.Type == PromoType.Percentage ? Math.Min(dto.Amount * promo.Value / 100, promo.MaxDiscount > 0 ? promo.MaxDiscount : decimal.MaxValue) : Math.Min(promo.Value, dto.Amount); return Results.Ok(new { valid = true, promo.Code, promo.Description, Type = promo.Type.ToString(), discount, finalAmount = dto.Amount - discount }); }).WithTags("Promos");

api.MapPost("/reviews", async (SubmitReviewRequest dto, FastRideDbContext db) => { var order = await db.Orders.FindAsync(dto.OrderId); if (order == null || order.Status != OrderStatus.Completed) return Results.BadRequest(new { error = "Order not completed" }); db.Reviews.Add(new Review { OrderId = dto.OrderId, ReviewerId = dto.ReviewerId, TargetUserId = dto.TargetUserId, Rating = dto.Rating, Comment = dto.Comment }); if (order.RiderId == dto.ReviewerId) order.DriverRating = dto.Rating; else order.RiderRating = dto.Rating; order.ReviewComment = dto.Comment; if (dto.TargetUserId != dto.ReviewerId) { var dp = await db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == dto.TargetUserId); if (dp != null) { var avg = await db.Reviews.Where(r => r.TargetUserId == dto.TargetUserId).AverageAsync(r => (double?)r.Rating) ?? dto.Rating; dp.Rating = Math.Round(avg, 1); } } await db.SaveChangesAsync(); return Results.Created($"/api/reviews/{dto.OrderId}", new { dto.OrderId, dto.Rating, dto.Comment }); }).WithTags("Reviews");

api.MapGet("/dashboard/stats", async (FastRideDbContext db) => { var today = DateTime.UtcNow.Date; return Results.Ok(new { totalOrdersToday = await db.Orders.CountAsync(o => o.CreatedAt >= today), activeDrivers = await db.DriverProfiles.CountAsync(dp => dp.Status == DriverStatus.Online || dp.Status == DriverStatus.OnTrip), revenueToday = await db.Orders.Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= today).SumAsync(o => o.FinalFare), averageRating = Math.Round(await db.DriverProfiles.AverageAsync(dp => (double?)dp.Rating) ?? 4.5, 1), pendingOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Requested), timestamp = DateTime.UtcNow }); }).WithTags("Dashboard");

api.MapGet("/dashboard/orders-by-status", async (FastRideDbContext db) => Results.Ok(await db.Orders.GroupBy(o => o.Status).Select(g => new { status = g.Key.ToString(), count = g.Count() }).ToListAsync())).WithTags("Dashboard");

api.MapGet("/dashboard/orders-by-hour", async (DateTime? date, FastRideDbContext db) => { var d = date?.Date ?? DateTime.UtcNow.Date; var r = new List<object>(); for (int h = 0; h < 24; h++) { var from = d.AddHours(h); var to = from.AddHours(1); r.Add(new { hour = h, count = db.Orders.Count(o => o.CreatedAt >= from && o.CreatedAt < to) }); } return Results.Ok(r); }).WithTags("Dashboard");

// Mobile endpoints
api.MapGet("/mobile/rider/{userId:guid}/home", async (Guid userId, FastRideDbContext db) => { var u = await db.Users.Where(x => x.Id == userId && x.Role == UserRole.Rider).Select(x => new { x.FullName, TotalTrips = x.RiderOrders.Count, TotalSpent = x.RiderOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalFare) }).FirstOrDefaultAsync(); if (u == null) return Results.NotFound(); var recent = await db.Orders.Where(o => o.RiderId == userId).OrderByDescending(o => o.CreatedAt).Take(10).Select(o => new { o.Id, DriverName = o.Driver != null ? o.Driver.FullName : null, o.PickupAddress, o.DropoffAddress, o.FinalFare, Status = o.Status.ToString(), o.CreatedAt }).ToListAsync(); return Results.Ok(new { userId, u.FullName, u.TotalTrips, u.TotalSpent, recentTrips = recent }); }).WithTags("Mobile-Rider");

api.MapGet("/mobile/driver/{userId:guid}/home", async (Guid userId, FastRideDbContext db) => { var d = await db.Users.Where(x => x.Id == userId && x.Role == UserRole.Driver).Include(x => x.DriverProfile).FirstOrDefaultAsync(); if (d?.DriverProfile == null) return Results.NotFound(); var today = DateTime.UtcNow.Date; var incoming = await db.Orders.Where(o => o.Status == OrderStatus.Requested && o.DriverId == null).OrderByDescending(o => o.CreatedAt).Take(5).Select(o => new { o.Id, RiderName = o.Rider.FullName, o.PickupAddress, o.DropoffAddress, o.DistanceKm, o.EstimatedFare, WaitSeconds = (int)(DateTime.UtcNow - o.CreatedAt).TotalSeconds }).ToListAsync(); return Results.Ok(new { driverId = userId, d.FullName, isOnline = d.DriverProfile.Status == DriverStatus.Online, todayEarnings = await db.Orders.Where(o => o.DriverId == userId && o.Status == OrderStatus.Completed && o.CompletedAt >= today).SumAsync(o => o.FinalFare), todayTrips = await db.Orders.CountAsync(o => o.DriverId == userId && o.Status == OrderStatus.Completed && o.CompletedAt >= today), d.DriverProfile.Rating, incomingOrders = incoming }); }).WithTags("Mobile-Driver");

api.MapGet("/mobile/driver/{userId:guid}/earnings", async (Guid userId, string? period, FastRideDbContext db) => { var now = DateTime.UtcNow; var start = period switch { "week" => now.AddDays(-7), "month" => now.AddMonths(-1), _ => now.Date }; var orders = await db.Orders.Where(o => o.DriverId == userId && o.Status == OrderStatus.Completed && o.CompletedAt >= start).ToListAsync(); var todayStart = now.Date; return Results.Ok(new { todayEarnings = orders.Where(o => o.CompletedAt >= todayStart).Sum(o => o.FinalFare), weekEarnings = orders.Where(o => o.CompletedAt >= now.AddDays(-7)).Sum(o => o.FinalFare), monthEarnings = orders.Sum(o => o.FinalFare), todayTrips = orders.Count(o => o.CompletedAt >= todayStart), dailyBreakdown = orders.GroupBy(o => o.CompletedAt!.Value.Date).Select(g => new { date = g.Key, earnings = g.Sum(x => x.FinalFare), trips = g.Count() }).OrderByDescending(x => x.date).Take(30).ToList() }); }).WithTags("Mobile-Driver");

api.MapPut("/mobile/driver/{userId:guid}/toggle-online", async (Guid userId, FastRideDbContext db) => { var dp = await db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == userId); if (dp == null) return Results.NotFound(); dp.Status = dp.Status == DriverStatus.Online ? DriverStatus.Offline : DriverStatus.Online; await db.SaveChangesAsync(); return Results.Ok(new { status = dp.Status.ToString() }); }).WithTags("Mobile-Driver");

api.MapPut("/mobile/driver/{userId:guid}/accept-order", async (Guid userId, AcceptOrderReq req, FastRideDbContext db) => { var order = await db.Orders.FindAsync(req.OrderId); if (order == null || order.Status != OrderStatus.Requested) return Results.BadRequest(new { error = "Order unavailable" }); order.DriverId = userId; order.Status = OrderStatus.Accepted; order.AcceptedAt = DateTime.UtcNow; var dp = await db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == userId); if (dp != null) dp.Status = DriverStatus.OnTrip; await db.SaveChangesAsync(); return Results.Ok(new { order.Id, Status = "Accepted" }); }).WithTags("Mobile-Driver");

api.MapPut("/mobile/driver/{userId:guid}/complete-order", async (Guid userId, AcceptOrderReq req, FastRideDbContext db) => { var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == req.OrderId && o.DriverId == userId); if (order == null) return Results.BadRequest(new { error = "Not your order" }); order.Status = OrderStatus.Completed; order.CompletedAt = DateTime.UtcNow; var dp = await db.DriverProfiles.FirstOrDefaultAsync(x => x.UserId == userId); if (dp != null) { dp.Status = DriverStatus.Online; dp.TotalTrips++; dp.TotalEarnings += order.FinalFare; } db.Payments.Add(new Payment { OrderId = order.Id, Amount = order.FinalFare, Method = order.PaymentMethod, Status = PaymentStatus.Completed, CompletedAt = DateTime.UtcNow, TransactionReference = $"TRX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}" }); await db.SaveChangesAsync(); return Results.Ok(new { order.Id, Status = "Completed" }); }).WithTags("Mobile-Driver");

api.MapGet("/notifications/{userId:guid}", async (Guid userId, FastRideDbContext db) => Results.Ok(await db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(50).Select(n => new { n.Id, n.Title, n.Message, Type = n.Type.ToString(), n.IsRead, n.CreatedAt }).ToListAsync())).WithTags("Notifications");

app.Run();

// ═══════════════ HELPERS ═══════════════
static string GenJwt(User user, IConfiguration config) { var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? "FastRide-Super-Secret-Key-Minimum-32-Characters-Long!")); var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.FullName), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString()) }; return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(config["Jwt:Issuer"] ?? "FastRide", config["Jwt:Audience"] ?? "FastRide", claims, expires: DateTime.UtcNow.AddDays(1), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256))); }
static string BCryptHash(string p) => BCrypt.Net.BCrypt.HashPassword(p, BCrypt.Net.BCrypt.GenerateSalt(12));
static bool BCryptVerify(string p, string h) { try { return BCrypt.Net.BCrypt.Verify(p, h); } catch { return false; } }
static double Haversine(double lat1, double lon1, double lat2, double lon2) { const double R = 6371; var dLat = (lat2 - lat1) * Math.PI / 180; var dLon = (lon2 - lon1) * Math.PI / 180; return R * 2 * Math.Atan2(Math.Sqrt(Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2)), Math.Sqrt(1 - Math.Sin(dLat / 2) * Math.Sin(dLat / 2) - Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2))); }
static string GenerateAvatarUrl(string fullName) { var initials = string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0]))); var color = new[] { "#FF6B35", "#FFD700", "#00C853", "#2979FF", "#AA00FF" }[Math.Abs(fullName.GetHashCode()) % 5]; var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'><rect width='200' height='200' rx='100' fill='{color}'/><text x='100' y='130' font-size='90' font-family='Arial' font-weight='bold' fill='white' text-anchor='middle'>{initials}</text></svg>"; return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}"; }

record RegisterDto([Required] string FullName, [Required, EmailAddress] string Email, [Required] string PhoneNumber, [Required, MinLength(8)] string Password, UserRole Role = UserRole.Rider, string? LicenseNumber = null, string? VehicleType = null, string? VehiclePlate = null);
record LoginDto([Required, EmailAddress] string Email, [Required] string Password);
record CreateOrderReq(Guid RiderId, double PickupLatitude, double PickupLongitude, string PickupAddress, double DropoffLatitude, double DropoffLongitude, string DropoffAddress, VehicleCategory VehicleCategory = VehicleCategory.Economy, PaymentMethod PaymentMethod = PaymentMethod.Cash, string? PromoCode = null);
record ValidatePromoDto([Required] string Code, decimal Amount);
record AcceptOrderReq(Guid OrderId);
record UpdateProfileReq(string? FullName, string? PhoneNumber, string? ProfilePhotoBase64, string? ProfilePhotoMimeType);
