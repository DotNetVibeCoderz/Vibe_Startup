using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Components.Authorization;
using Joka.Data;
using Joka.Services;
using Joka.Services.Chat;
using Joka.Services.Storage;
using Joka.Components;
using Joka.Models.Chat;
using Joka.Models.Users;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Without persisted keys the app generates a fresh set on every start, which
// invalidates every antiforgery token and auth cookie already in a browser -
// a login page left open across a restart fails with "A valid antiforgery
// token was not provided". Also required if this ever runs on more than one
// instance, since they must share keys.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "Data", "keys")))
    .SetApplicationName("Joka");

// AppSettings:DefaultLanguage is "id", but without setting the culture every
// price formatted with "N0" renders as "Rp1,200,000" instead of "Rp1.200.000".
var defaultCulture = new System.Globalization.CultureInfo(
    builder.Configuration["AppSettings:DefaultLanguage"] == "en" ? "en-US" : "id-ID");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// ============================================
// DATABASE CONFIGURATION
// ============================================
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString(dbProvider)
    ?? builder.Configuration[$"Database:ConnectionStrings:{dbProvider}"];

// Added after appsettings so database overrides win. Every existing
// IConfiguration read picks them up without touching the call sites.
((IConfigurationBuilder)builder.Configuration)
    .Add(new Joka.Data.DatabaseConfigurationSource(dbProvider, connectionString ?? ""));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SQLServer":
            options.UseSqlServer(connectionString);
            break;
        case "MySQL":
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            break;
        case "Postgre":
            options.UseNpgsql(connectionString);
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
});

// ============================================
// AUTHENTICATION & AUTHORIZATION
// ============================================
var googleClientId = builder.Configuration["AppSettings:Auth:GoogleClientId"];
var googleClientSecret = builder.Configuration["AppSettings:Auth:GoogleClientSecret"];
var googleEnabled = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
    // Only challenge via Google when it is actually configured, otherwise
    // fall back to the cookie scheme's login page.
    options.DefaultChallengeScheme = googleEnabled ? "Google" : "Cookies";
})
.AddCookie("Cookies", options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Registering Google with empty credentials makes OAuthOptions.Validate() throw
// on every request, because the authentication middleware initialises remote
// handlers for each request. Register it only when it is usable.
if (googleEnabled)
{
    authBuilder.AddGoogle("Google", options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.CallbackPath = "/signin-google";
        options.SaveTokens = true;
    });
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(Joka.Models.Backoffice.Roles.Admin))
    .AddPolicy("OperatorArea", p => p.RequireRole(
        Joka.Models.Backoffice.Roles.Admin, Joka.Models.Backoffice.Roles.Operator))
    .AddPolicy("MerchantArea", p => p.RequireRole(
        Joka.Models.Backoffice.Roles.Admin, Joka.Models.Backoffice.Roles.Merchant))
    .AddPolicy("BackOffice", p => p.RequireRole(
        Joka.Models.Backoffice.Roles.Admin,
        Joka.Models.Backoffice.Roles.Operator,
        Joka.Models.Backoffice.Roles.Merchant));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// ============================================
// SERVICES
// ============================================

// Auth Service
builder.Services.AddScoped<AuthService>();

// Checkout: voucher validation, PayLater, transactions, loyalty points
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<FraudDetectionService>();
builder.Services.AddScoped<HealthProbeService>();
builder.Services.AddScoped<MerchantCatalogService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<ReviewService>();

// Payment gateways. All three are registered; the factory decides which one is
// actually reachable, falling back to the stub when credentials are missing.
builder.Services.AddScoped<Joka.Services.Payments.StubGateway>();
builder.Services.AddScoped<Joka.Services.Payments.MidtransGateway>();
builder.Services.AddScoped<Joka.Services.Payments.XenditGateway>();
builder.Services.AddScoped<Joka.Services.Payments.PaymentGatewayFactory>();
builder.Services.AddSingleton<CurrencyService>();
builder.Services.AddScoped<UserPreferences>();
builder.Services.AddSingleton<NotificationBroadcaster>();
builder.Services.AddScoped<SupportService>();
builder.Services.AddScoped<TransportService>();

// Train schedules: local by default, KAI when Integrations:KAI:ApiKey is filled.
builder.Services.AddScoped<Joka.Services.Trains.LocalTrainScheduleProvider>();
builder.Services.AddScoped<Joka.Services.Trains.KaiTrainScheduleProvider>();
builder.Services.AddScoped<Joka.Services.Trains.TrainScheduleService>();
builder.Services.AddSingleton<SupportBroadcaster>();

// Storage
builder.Services.AddScoped<FileSystemStorageService>();
builder.Services.AddSingleton<StorageServiceFactory>();

// Markdown
builder.Services.AddSingleton<MarkdownService>();

// ChatBot
builder.Services.AddScoped<ChatBotService>();
builder.Services.AddHttpClient();

// SignalR
builder.Services.AddSignalR();

// ============================================
// BLAZOR & RAZOR COMPONENTS
// ============================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ============================================
// SWAGGER / OPENAPI
// ============================================
// EF navigation properties point both ways (Hotel <-> Room), which makes the
// serializer recurse until it throws. Ignore cycles instead of dropping the
// Include()s that the endpoints rely on.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = builder.Configuration["Swagger:Title"] ?? "Joka OTA API",
        Version = builder.Configuration["Swagger:Version"] ?? "v1",
        Description = builder.Configuration["Swagger:Description"] ?? "REST API for Joka OTA"
    });
});

// No ResourcesPath: MSBuild names the embedded resource from the namespace of
// the neighbouring SharedResource.cs (Joka), giving "Joka.SharedResource".
// Setting ResourcesPath would make the localizer look for
// "Joka.Resources.SharedResource" and silently fall back to echoing the keys.
builder.Services.AddLocalization();

// Indonesian is the neutral resource, so it is both the default and the
// fallback: a key with no English translation renders the Indonesian text
// rather than the raw key.
var supportedCultures = new[]
{
    new System.Globalization.CultureInfo("id"),
    new System.Globalization.CultureInfo("en")
};

builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("id");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Cookie first: an explicit choice must beat the browser's Accept-Language.
    options.RequestCultureProviders = new List<Microsoft.AspNetCore.Localization.IRequestCultureProvider>
    {
        new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider(),
        new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider()
    };
});

var app = builder.Build();

// ============================================
// DATABASE INITIALIZATION
// ============================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    await SeedData.InitializeAsync(dbContext);
}

// ============================================
// MIDDLEWARE PIPELINE
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Joka OTA API v1"));
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Must run before anything renders, so the culture is already set when
// components resolve their strings.
app.UseRequestLocalization();

app.UseHttpsRedirection();
app.UseStaticFiles();
// A stale form (tab left open, restored from cache, cookies cleared) must not
// dump the raw framework text on the user. The antiforgery middleware writes a
// plain 400 body rather than throwing, so the response has to be buffered to
// catch it. Scoped to form posts - API responses stream through untouched.
app.Use(async (context, next) =>
{
    var isFormPost = HttpMethods.IsPost(context.Request.Method)
        && !context.Request.Path.StartsWithSegments("/api");

    if (!isFormPost)
    {
        await next();
        return;
    }

    var originalBody = context.Response.Body;
    using var buffer = new MemoryStream();
    context.Response.Body = buffer;

    try
    {
        await next();

        context.Response.Body = originalBody;

        buffer.Position = 0;
        var isAntiforgeryFailure = context.Response.StatusCode == StatusCodes.Status400BadRequest
            && new StreamReader(buffer).ReadToEnd()
                .Contains("antiforgery token", StringComparison.OrdinalIgnoreCase);

        if (isAntiforgeryFailure)
        {
            var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
            var message = Uri.EscapeDataString("Sesi formulir sudah kedaluwarsa. Silakan coba lagi.");

            context.Response.Clear();
            context.Response.Redirect($"{path}?error={message}");
            return;
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
    }
    finally
    {
        context.Response.Body = originalBody;
    }
});

app.UseAntiforgery();

// Auth middleware
app.UseAuthentication();
app.UseAuthorization();

// ============================================
// MINIMAL API ENDPOINTS
// ============================================
var apiGroup = app.MapGroup("/api");

// --- Auth ---
apiGroup.MapPost("/auth/register", async (RegisterRequest req, AuthService auth) =>
{
    var (success, message, user) = await auth.RegisterAsync(req);
    return success ? Results.Ok(new { message, userId = user!.Id }) : Results.BadRequest(new { message });
});

apiGroup.MapPost("/auth/login", async (LoginRequest req, AuthService auth) =>
{
    var (success, message, user) = await auth.LoginAsync(req);
    return success ? Results.Ok(new { message, userId = user!.Id, user.Username, user.Email, user.FullName }) : Results.BadRequest(new { message });
});

apiGroup.MapPost("/auth/reset-password-request", async (ResetPasswordRequest req, AuthService auth) =>
{
    var (success, message) = await auth.RequestPasswordResetAsync(req.Email);
    return Results.Ok(new { message });
});

apiGroup.MapPost("/auth/reset-password", async (ChangePasswordRequest req, AuthService auth) =>
{
    var (success, message) = await auth.ResetPasswordAsync(req);
    return success ? Results.Ok(new { message }) : Results.BadRequest(new { message });
});

apiGroup.MapGet("/auth/profile", async (HttpContext ctx, AuthService auth) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Unauthorized();
    var user = await auth.GetUserByIdAsync(Guid.Parse(userIdClaim));
    return user != null ? Results.Ok(new { user.Username, user.Email, user.FullName, user.AvatarUrl, user.LoyaltyPoints, user.MembershipTier, user.PhoneNumber }) : Results.NotFound();
});

// --- Flights ---
apiGroup.MapGet("/flights", async (AppDbContext db, string? from, string? to, DateTime? date, int? maxPrice, string? sort) =>
{
    var query = db.Flights.Include(f => f.Airline).Include(f => f.DepartureAirport).Include(f => f.ArrivalAirport).AsQueryable();
    if (!string.IsNullOrEmpty(from)) query = query.Where(f => f.DepartureAirport!.Code == from.ToUpper());
    if (!string.IsNullOrEmpty(to)) query = query.Where(f => f.ArrivalAirport!.Code == to.ToUpper());
    if (date.HasValue) query = query.Where(f => f.DepartureTime.Date == date.Value.Date);
    if (maxPrice.HasValue) query = query.Where(f => f.BasePrice <= maxPrice.Value);
    query = sort switch
    {
        "price_desc" => query.OrderByDescending(f => f.BasePrice),
        "departure" => query.OrderBy(f => f.DepartureTime),
        "duration" => query.OrderBy(f => f.DurationMinutes),
        _ => query.OrderBy(f => f.BasePrice)
    };
    return Results.Ok(await query.Take(100).ToListAsync());
});

// --- Hotels ---
apiGroup.MapGet("/hotels", async (AppDbContext db, string? city, int? minStars, int? maxPrice) =>
{
    var query = db.Hotels.Include(h => h.Rooms).AsQueryable();
    if (!string.IsNullOrEmpty(city)) query = query.Where(h => h.City.Contains(city));
    if (minStars.HasValue) query = query.Where(h => h.StarRating >= minStars.Value);
    var results = await query.Take(50).ToListAsync();
    if (maxPrice.HasValue) results = results.Where(h => h.Rooms.Any(r => r.PricePerNight <= maxPrice.Value)).ToList();
    return Results.Ok(results);
});

apiGroup.MapGet("/hotels/{id:guid}", async (AppDbContext db, Guid id) =>
{
    var hotel = await db.Hotels.Include(h => h.Rooms).Include(h => h.Reviews).FirstOrDefaultAsync(h => h.Id == id);
    return hotel is not null ? Results.Ok(hotel) : Results.NotFound();
});

// --- Trains ---
apiGroup.MapGet("/trains", async (AppDbContext db, string? from, string? to, DateTime? date) =>
{
    var query = db.TrainSchedules.Include(t => t.Train).Include(t => t.DepartureStation).Include(t => t.ArrivalStation).AsQueryable();
    if (!string.IsNullOrEmpty(from)) query = query.Where(t => t.DepartureStation!.Code == from.ToUpper());
    if (!string.IsNullOrEmpty(to)) query = query.Where(t => t.ArrivalStation!.Code == to.ToUpper());
    if (date.HasValue) query = query.Where(t => t.DepartureTime.Date == date.Value.Date);
    return Results.Ok(await query.OrderBy(t => t.BasePrice).Take(50).ToListAsync());
});

// --- Buses & shuttles ---
apiGroup.MapGet("/buses", async (AppDbContext db, string? from, string? to, DateTime? date, string? type) =>
{
    var query = db.BusSchedules.AsNoTracking()
        .Include(s => s.BusService!).ThenInclude(b => b.Operator)
        .Include(s => s.DepartureTerminal)
        .Include(s => s.ArrivalTerminal)
        .Where(s => s.IsActive)
        .AsQueryable();

    if (!string.IsNullOrEmpty(from)) query = query.Where(s => s.DepartureTerminal!.Code == from.ToUpper());
    if (!string.IsNullOrEmpty(to)) query = query.Where(s => s.ArrivalTerminal!.Code == to.ToUpper());
    if (!string.IsNullOrEmpty(type)) query = query.Where(s => s.BusService!.ServiceType == type);
    if (date.HasValue) query = query.Where(s => s.DepartureTime.Date == date.Value.Date);

    return Results.Ok(await query.OrderBy(s => s.BasePrice).Take(50).ToListAsync());
});

apiGroup.MapGet("/bus-terminals", async (AppDbContext db) =>
    Results.Ok(await db.BusTerminals.AsNoTracking().OrderBy(t => t.City).ToListAsync()));

// --- Activities ---
apiGroup.MapGet("/activities", async (AppDbContext db, string? city, string? category) =>
{
    var query = db.Activities.AsQueryable();
    if (!string.IsNullOrEmpty(city)) query = query.Where(a => a.City.Contains(city));
    if (!string.IsNullOrEmpty(category)) query = query.Where(a => a.Category == category);
    return Results.Ok(await query.Take(50).ToListAsync());
});

// --- Promos ---
apiGroup.MapGet("/promos", async (AppDbContext db) =>
{
    var now = DateTime.UtcNow;
    return Results.Ok(await db.PromoVouchers.Where(v => v.IsActive && v.ValidFrom <= now && v.ValidUntil >= now && v.UsedCount < v.TotalQuota).ToListAsync());
});

// --- Insurance ---
apiGroup.MapGet("/insurance", async (AppDbContext db) =>
    Results.Ok(await db.TravelInsurances.Where(i => i.IsActive).ToListAsync()));

// --- Chat ---
apiGroup.MapPost("/chat/send", async (ChatRequest req, ChatBotService chat) =>
{
    var resp = await chat.SendMessageAsync(req.Message, req.Attachments);
    return Results.Ok(resp);
});
apiGroup.MapPost("/chat/reset", (ChatBotService chat) => { chat.ResetSession(); return Results.Ok(new { message = "ok" }); });

// --- Local transport ---
// Nama query string sengaja sama persis dengan nama field di respons, supaya
// pemanggil bisa menyaring pakai istilah yang baru saja dia baca dari hasilnya.
apiGroup.MapGet("/transport", async (TransportService transport, string? city, string? serviceType,
    string? vehicleType, double? distanceKm, string? airportCode) =>
{
    var quotes = await transport.SearchAsync(city, serviceType, vehicleType, distanceKm ?? 8, airportCode);

    return Results.Ok(quotes.Select(q => new
    {
        id = q.Option.Id,
        name = q.Option.Name,
        provider = q.Option.Provider?.Name,
        serviceType = q.Option.ServiceType,
        vehicleType = q.Option.VehicleType,
        city = q.Option.City,
        capacity = q.Option.Capacity,
        routeArea = q.Option.RouteArea,
        airportCode = q.Option.AirportCode,
        fare = q.Fare,
        estimatedMinutes = q.EstimatedMinutes
    }));
});

// --- Payment gateway webhooks ---
// These are the ONLY paths that may mark a transaction paid. Both verify the
// provider's signature first: without that check anyone who guessed an order id
// could settle it for free.
apiGroup.MapPost("/payments/midtrans-notification", async (
    HttpContext ctx,
    Joka.Services.Payments.MidtransGateway midtrans,
    CheckoutService checkout,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("MidtransWebhook");

    if (!midtrans.IsConfigured)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;

    string? Read(string name) =>
        root.TryGetProperty(name, out var v) ? v.GetString() : null;

    var orderId = Read("order_id");
    var statusCode = Read("status_code");
    var grossAmount = Read("gross_amount");
    var signature = Read("signature_key");

    if (orderId is null || statusCode is null || grossAmount is null || signature is null)
        return Results.BadRequest(new { message = "Payload tidak lengkap." });

    if (!midtrans.VerifySignature(orderId, statusCode, grossAmount, signature))
    {
        log.LogWarning("Signature Midtrans tidak cocok untuk {OrderId}", orderId);
        return Results.Unauthorized();
    }

    var status = Joka.Services.Payments.MidtransGateway.MapStatus(
        Read("transaction_status"), Read("fraud_status"));

    var result = await checkout.SettleAsync(orderId, status, Read("transaction_id"));

    log.LogInformation("Midtrans {OrderId} -> {Status}: {Message}", orderId, status, result.Message);

    // 200 even when the code is unknown to us, otherwise Midtrans retries forever.
    return Results.Ok(new { received = true, status });
});

apiGroup.MapPost("/payments/xendit-callback", async (
    HttpContext ctx,
    Joka.Services.Payments.XenditGateway xendit,
    CheckoutService checkout,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("XenditWebhook");

    if (!xendit.IsConfigured)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    if (!xendit.VerifyCallbackToken(ctx.Request.Headers["x-callback-token"]))
    {
        log.LogWarning("Callback token Xendit tidak cocok.");
        return Results.Unauthorized();
    }

    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;

    var externalId = root.TryGetProperty("external_id", out var e) ? e.GetString() : null;
    var rawStatus = root.TryGetProperty("status", out var s) ? s.GetString() : null;
    var invoiceId = root.TryGetProperty("id", out var i) ? i.GetString() : null;

    if (externalId is null)
        return Results.BadRequest(new { message = "external_id wajib ada." });

    var status = Joka.Services.Payments.XenditGateway.MapStatus(rawStatus);
    var result = await checkout.SettleAsync(externalId, status, invoiceId);

    log.LogInformation("Xendit {ExternalId} -> {Status}: {Message}", externalId, status, result.Message);

    return Results.Ok(new { received = true, status });
});

// Which gateway is live, for the admin Settings screen. Never returns the keys.
apiGroup.MapGet("/payments/gateway", (Joka.Services.Payments.PaymentGatewayFactory factory) =>
    Results.Ok(new { provider = factory.ActiveProvider, live = factory.IsLive }));

// --- Config ---
apiGroup.MapGet("/config", (IConfiguration config) => Results.Ok(new
{
    name = config["AppSettings:AppName"], tagline = config["AppSettings:AppTagline"],
    version = config["AppSettings:AppVersion"], theme = config["AppSettings:DefaultTheme"],
    languages = config.GetSection("AppSettings:SupportedLanguages").Get<string[]>(),
    currencies = config.GetSection("AppSettings:SupportedCurrencies").Get<string[]>()
}));

// --- Dashboard ---
apiGroup.MapGet("/dashboard/stats", async (AppDbContext db) => Results.Ok(new
{
    totalFlights = await db.Flights.CountAsync(), totalHotels = await db.Hotels.CountAsync(),
    totalActivities = await db.Activities.CountAsync(), totalUsers = await db.Users.CountAsync(),
    flightsToday = await db.Flights.CountAsync(f => f.DepartureTime.Date == DateTime.UtcNow.Date)
}));

// --- User Bookings ---
apiGroup.MapGet("/my/bookings", async (HttpContext ctx, AuthService auth) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Unauthorized();
    var uid = Guid.Parse(userIdClaim);
    return Results.Ok(new
    {
        flights = await auth.GetUserFlightBookingsAsync(uid),
        hotels = await auth.GetUserHotelBookingsAsync(uid),
        trains = await auth.GetUserTrainBookingsAsync(uid)
    });
});

apiGroup.MapGet("/my/wishlist", async (HttpContext ctx, AuthService auth) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Unauthorized();
    return Results.Ok(await auth.GetUserWishlistAsync(Guid.Parse(userIdClaim)));
});

apiGroup.MapGet("/my/notifications", async (HttpContext ctx, AuthService auth) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Unauthorized();
    return Results.Ok(await auth.GetUserNotificationsAsync(Guid.Parse(userIdClaim)));
});


// ============================================
// GOOGLE OAUTH & LOGOUT ENDPOINTS
// ============================================

// Google login initiation
app.MapPost("/login-google", async (HttpContext ctx) =>
{
    if (!googleEnabled)
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Login Google belum dikonfigurasi."));

    await ctx.ChallengeAsync("Google", new AuthenticationProperties
    {
        RedirectUri = "/google-callback",
        Items = { { "scheme", "Google" } }
    });
    return Results.Empty;
});

// Google OAuth callback
app.MapGet("/google-callback", async (HttpContext ctx, AuthService authService) =>
{
    var result = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    if (!result.Succeeded)
        return Results.Redirect("/login?error=google-failed");

    var claims = result.Principal?.Claims;
    var googleId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
    var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "";
    var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "";
    var avatar = ""; // Google doesn't always provide avatar in claims

    var (success, message, user) = await authService.GoogleLoginAsync(googleId, email, name, avatar);
    if (!success || user == null)
        return Results.Redirect($"/login?error={Uri.EscapeDataString(message)}");

    // Sign in with cookie
    var cookieClaims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.GivenName, user.FullName ?? user.Username),
        new("AvatarUrl", user.AvatarUrl ?? ""),
        new("MembershipTier", user.MembershipTier),
        new(ClaimTypes.Role, user.Role)
    };
    var identity = new ClaimsIdentity(cookieClaims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

    return Results.Redirect("/");
});

// Logout
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

// ============================================
// ============================================
// BLAZOR ROUTING
// ============================================
// Preference switcher. A form post + redirect rather than JS, so the choice is
// already in the cookie when the next request renders server-side.
app.MapPost("/set-preference", (HttpContext ctx, string? currency, string? culture, string? returnUrl) =>
{
    var options = new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        IsEssential = true,
        HttpOnly = false,
        SameSite = SameSiteMode.Lax
    };

    if (!string.IsNullOrWhiteSpace(currency))
        ctx.Response.Cookies.Append(UserPreferences.CurrencyCookie, currency, options);

    if (!string.IsNullOrWhiteSpace(culture))
    {
        ctx.Response.Cookies.Append(
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,
            Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(
                new Microsoft.AspNetCore.Localization.RequestCulture(culture)),
            options);
    }

    // Only ever bounce back to a local path - an open redirect here would be real.
    var destination = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') ? returnUrl : "/";
    return Results.Redirect(destination);
}).DisableAntiforgery();

app.MapHub<Joka.Hubs.NotificationHub>("/hubs/notifications");

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();


