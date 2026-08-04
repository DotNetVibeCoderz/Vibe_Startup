using FastRide.Api.Endpoints;
using FastRide.Api.Extensions;
using FastRide.Api.Infrastructure;
using FastRide.Api.Payments;
using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.Storage;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════ services ═══════════════════════════════

builder.Services
    .AddFastRideJson()
    .AddFastRideDatabase(builder.Configuration)
    .AddFastRideCache(builder.Configuration)
    .AddFastRideStorage()
    .AddFastRideAuth(builder.Configuration)
    .AddFastRideCors(builder.Configuration)
    .AddFastRideRateLimiting(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DispatchService>();
builder.Services.AddScoped<PaymentProviderRegistry>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ═══════════════════════════ database bootstrap ═══════════════════════════

await InitialiseDatabaseAsync(app);

// ═════════════════════════════ middleware ═════════════════════════════

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    app.Logger.LogError(feature?.Error, "Unhandled exception on {Method} {Path}.",
        context.Request.Method, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";

    // Exception text can carry connection strings and file paths; only development sees it.
    await context.Response.WriteAsJsonAsync(new ApiError(
        "ServerError",
        app.Environment.IsDevelopment() ? feature?.Error.Message : "Terjadi kesalahan pada server."));
}));

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseResponseCompression();

// Turn this off when something in front already terminates TLS — a reverse proxy, or the
// in-memory test host. Following a scheme change makes clients drop the Authorization
// header, so every authenticated request would arrive anonymous.
if (app.Configuration.GetValue("ApiSettings:UseHttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseCors("AllowClients");

// Uploads are served from wherever the storage provider actually writes them, rather than
// assuming a folder under wwwroot that may not exist.
if (app.Services.GetRequiredService<IStorageProvider>() is FileSystemStorageProvider fileStorage)
{
    Directory.CreateDirectory(fileStorage.RootPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(fileStorage.RootPath),
        RequestPath = fileStorage.RequestPath
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SecurityStampMiddleware>();
app.UseAuthorization();

// ══════════════════════════════ endpoints ══════════════════════════════

var api = app.MapGroup("/api");

api.MapAdminEndpoints()
   .MapAuthEndpoints()
   .MapProfileEndpoints()
   .MapRiderEndpoints()
   .MapDriverEndpoints()
   .MapOrderEndpoints()
   .MapPaymentEndpoints()
   .MapPaymentProviderEndpoints()
   .MapCatalogEndpoints()
   .MapNotificationEndpoints()
   .MapDashboardEndpoints();

// Standing in for the payer is a development affordance, not something a live deployment
// should expose — it would let anyone mark their own trip paid.
if (!app.Environment.IsProduction()) api.MapPaymentSandboxEndpoints();

app.Run();

// ═══════════════════════════════ helpers ═══════════════════════════════

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FastRideDbContext>();

    await db.Database.EnsureCreatedAsync();

    // EnsureCreated only builds a schema when the database is missing entirely. If an older
    // FastRide.db is lying around, queries fail later with a confusing "no such column".
    // Probing here turns that into an instruction the developer can act on.
    try
    {
        await db.Users.AsNoTracking().Select(u => new { u.Id, u.SecurityStamp }).FirstOrDefaultAsync();
        await db.Orders.AsNoTracking().Select(o => new { o.Id, o.Code }).FirstOrDefaultAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "The existing database does not match the current model. Delete the database file " +
            "(FastRide.db by default) and start again — the sample data will be reseeded. " +
            "See docs/DATABASE.md.");
        throw;
    }

    // Providers declared in appsettings become editable rows; existing rows are left alone,
    // so an operator's choices in the console outrank the file.
    await PaymentProviderRegistry.SeedFromConfigurationAsync(db, app.Configuration, app.Logger);

    if (app.Configuration.GetValue("Database:AutoSeed", true))
        await SampleDataSeeder.SeedAsync(db);
}

/// <summary>
/// Top-level statements generate an internal Program class. Declaring it here makes it
/// public so the integration tests can host the real application through
/// WebApplicationFactory&lt;Program&gt; instead of re-creating the pipeline.
/// </summary>
public partial class Program;
