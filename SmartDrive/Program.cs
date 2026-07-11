using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartDrive.Components;
using SmartDrive.Data;
using SmartDrive.Data.Seed;
using SmartDrive.Models.Entities;
using SmartDrive.Services;
using SmartDrive.Models.Enums;

var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext().WriteTo.Console().WriteTo.File("logs/smartdrive-.txt", rollingInterval: RollingInterval.Day).CreateLogger();
builder.Host.UseSerilog();

var dbProvider = builder.Configuration.GetValue("Database:Provider", "SQLite");
builder.Services.AddDbContext<SmartDriveDbContext>((sp, options) =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    switch (dbProvider?.ToLower())
    {
        case "sqlserver": options.UseSqlServer(cs, o => o.MigrationsAssembly("SmartDrive")); break;
        case "mysql": options.UseMySql(cs, ServerVersion.AutoDetect(cs), o => o.MigrationsAssembly("SmartDrive")); break;
        case "postgresql": options.UseNpgsql(cs, o => o.MigrationsAssembly("SmartDrive")); break;
        default: options.UseSqlite(cs ?? "Data Source=smartdrive.db", o => o.MigrationsAssembly("SmartDrive")); break;
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequireDigit = true; o.Password.RequiredLength = 6; o.Password.RequireNonAlphanumeric = false; o.Password.RequireUppercase = true;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); o.Lockout.MaxFailedAccessAttempts = 5; o.User.RequireUniqueEmail = true; o.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<SmartDriveDbContext>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o => { o.Cookie.HttpOnly = true; o.ExpireTimeSpan = TimeSpan.FromDays(7); o.LoginPath = "/auth/login"; o.LogoutPath = "/auth/logout"; o.AccessDeniedPath = "/auth/access-denied"; o.SlidingExpiration = true; });

builder.Services.AddHttpClient();
builder.Services.AddScoped<StorageService>(); builder.Services.AddScoped<ExportService>(); builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton<ChatBotService>();

// GpsSimulatorService: register sebagai Singleton + HostedService agar bisa di-inject
builder.Services.AddSingleton<GpsSimulatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GpsSimulatorService>());

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "SmartDrive API", Version = "v1" }));

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); } else { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/not-found"); app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseAntiforgery(); app.UseAuthentication(); app.UseAuthorization();

// AUTH
app.MapPost("/api/auth/login", async (HttpContext ctx) => {
    var f = await ctx.Request.ReadFormAsync(); var em = f["email"].FirstOrDefault(); var pw = f["password"].FirstOrDefault(); var rm = f["rememberMe"].FirstOrDefault(); var ru = f["returnUrl"].FirstOrDefault();
    if (string.IsNullOrEmpty(em) || string.IsNullOrEmpty(pw)) { ctx.Response.Redirect($"/auth/login?error={Uri.EscapeDataString("Email dan password harus diisi.")}"); return; }
    var um = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>(); var sm = ctx.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
    var user = await um.FindByEmailAsync(em);
    if (user == null) { ctx.Response.Redirect($"/auth/login?error={Uri.EscapeDataString("Email atau password salah.")}"); return; }
    var result = await sm.PasswordSignInAsync(user, pw ?? "", rm == "true", lockoutOnFailure: true);
    if (result.Succeeded) { user.LastLoginAt = DateTime.UtcNow; await um.UpdateAsync(user); var redir = string.IsNullOrEmpty(ru) || ru == "/" ? user.Role switch { UserRole.Admin => "/admin/dashboard", UserRole.Instructor => "/instructor/dashboard", _ => "/student/dashboard" } : ru; ctx.Response.Redirect(redir); }
    else if (result.IsLockedOut) ctx.Response.Redirect($"/auth/login?error={Uri.EscapeDataString("Akun terkunci.")}");
    else ctx.Response.Redirect($"/auth/login?error={Uri.EscapeDataString("Email atau password salah.")}");
}).DisableAntiforgery();

app.MapPost("/api/auth/register", async (HttpContext ctx) => {
    var f = await ctx.Request.ReadFormAsync(); var fn = f["fullName"].FirstOrDefault(); var em = f["email"].FirstOrDefault(); var ph = f["phoneNumber"].FirstOrDefault(); var rl = f["role"].FirstOrDefault(); var pw = f["password"].FirstOrDefault(); var cp = f["confirmPassword"].FirstOrDefault();
    if (pw != cp) { ctx.Response.Redirect("/auth/register?error=" + Uri.EscapeDataString("Password tidak cocok.")); return; }
    var um = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>(); var sm = ctx.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
    var role = rl == "Instructor" ? UserRole.Instructor : UserRole.Student;
    if (await um.FindByEmailAsync(em ?? "") != null) { ctx.Response.Redirect("/auth/register?error=" + Uri.EscapeDataString("Email sudah terdaftar.")); return; }
    var user = new ApplicationUser { UserName = em, Email = em, FullName = fn ?? "", PhoneNumber = ph, Role = role, IsActive = true, CreatedAt = DateTime.UtcNow, EmailConfirmed = true };
    var result = await um.CreateAsync(user, pw ?? "");
    if (result.Succeeded) { await um.AddToRoleAsync(user, role.ToString()); if (role == UserRole.Student) user.StudentProfile = new StudentProfile { UserId = user.Id, EnrollmentDate = DateTime.UtcNow }; else user.InstructorProfile = new InstructorProfile { UserId = user.Id, IsAvailable = true }; await sm.SignInAsync(user, false); ctx.Response.Redirect(role == UserRole.Instructor ? "/instructor/dashboard" : "/student/dashboard"); }
    else ctx.Response.Redirect("/auth/register?error=" + Uri.EscapeDataString(string.Join(", ", result.Errors.Select(e => e.Description))));
}).DisableAntiforgery();

app.MapPost("/api/auth/forgot-password", async (HttpContext ctx) => { var f = await ctx.Request.ReadFormAsync(); var em = f["email"].FirstOrDefault(); var um = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>(); var u = await um.FindByEmailAsync(em ?? ""); if (u != null) { var t = await um.GeneratePasswordResetTokenAsync(u); ctx.Response.Redirect($"/auth/forgot-password?msg={Uri.EscapeDataString($"Link reset: <a href='/auth/reset-password?email={Uri.EscapeDataString(em!)}&token={Uri.EscapeDataString(t)}'>Klik di sini</a>")}"); } else ctx.Response.Redirect("/auth/forgot-password?msg=" + Uri.EscapeDataString("Email tidak ditemukan.")); }).DisableAntiforgery();
app.MapPost("/api/auth/reset-password", async (HttpContext ctx) => { var f = await ctx.Request.ReadFormAsync(); var em = f["email"].FirstOrDefault(); var tk = f["token"].FirstOrDefault(); var np = f["newPassword"].FirstOrDefault(); var cp = f["confirmPassword"].FirstOrDefault(); if (np != cp) { ctx.Response.Redirect($"/auth/reset-password?email={Uri.EscapeDataString(em??"")}&token={Uri.EscapeDataString(tk??"")}&msg={Uri.EscapeDataString("Password tidak cocok.")}&ok=false"); return; } var um = ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>(); var u = await um.FindByEmailAsync(em ?? ""); if (u == null) { ctx.Response.Redirect("/auth/reset-password?msg=" + Uri.EscapeDataString("Email tidak ditemukan.") + "&ok=false"); return; } var r = await um.ResetPasswordAsync(u, tk ?? "", np ?? ""); if (r.Succeeded) ctx.Response.Redirect("/auth/reset-password?msg=" + Uri.EscapeDataString("Password berhasil direset! <a href='/auth/login'>Login sekarang</a>") + "&ok=true"); else ctx.Response.Redirect($"/auth/reset-password?email={Uri.EscapeDataString(em??"")}&token={Uri.EscapeDataString(tk??"")}&msg={Uri.EscapeDataString(string.Join(", ", r.Errors.Select(e=>e.Description)))}&ok=false"); }).DisableAntiforgery();
app.MapPost("/auth/logout", async (HttpContext ctx) => { await ctx.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>().SignOutAsync(); ctx.Response.Redirect("/"); });

// DATA API
var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));
api.MapGet("/vehicles", async (SmartDriveDbContext db) => Results.Ok(await db.Vehicles.OrderBy(v => v.Id).ToListAsync()));
api.MapGet("/locations", async (SmartDriveDbContext db) => Results.Ok(await db.TrainingLocations.Where(l => l.IsActive).OrderBy(l => l.Name).ToListAsync()));
api.MapGet("/bookings", async (SmartDriveDbContext db) => Results.Ok(await db.Bookings.Include(b => b.Student).Include(b => b.Instructor).OrderByDescending(b => b.StartTime).Take(50).ToListAsync()));
api.MapPost("/gps/push", async (SmartDriveDbContext db, GpsTrackingData d) => { d.Timestamp = DateTime.UtcNow; db.GpsTrackingData.Add(d); await db.SaveChangesAsync(); return Results.Created($"/api/gps/{d.Id}", d); });
api.MapGet("/gps/{bid}", async (SmartDriveDbContext db, int bid) => Results.Ok(await db.GpsTrackingData.Where(g => g.BookingId == bid).OrderBy(g => g.Timestamp).ToListAsync()));
api.MapGet("/payments", async (SmartDriveDbContext db) => Results.Ok(await db.Payments.Include(p => p.User).OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync()));

// PAYMENT VERIFICATION API (admin)
api.MapPost("/api/payments/{id}/verify", async (SmartDriveDbContext db, int id) => {
    var p = await db.Payments.FindAsync(id);
    if (p == null) return Results.NotFound();
    p.Status = PaymentStatus.Paid; p.PaidAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(p);
});
api.MapPost("/api/payments/{id}/reject", async (SmartDriveDbContext db, int id) => {
    var p = await db.Payments.FindAsync(id);
    if (p == null) return Results.NotFound();
    p.Status = PaymentStatus.Failed;
    await db.SaveChangesAsync();
    return Results.Ok(p);
});

// Seed
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("SeedData", false)) await DbSeeder.SeedAsync(app.Services);

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
Log.Information("SmartDrive Academy starting up...");
app.Run();
