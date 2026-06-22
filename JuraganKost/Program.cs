using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;
using JuraganKost.Services;
using JuraganKost.Services.Storage;
using JuraganKost.Services.Chat;
using JuraganKost.Components;

var builder = WebApplication.CreateBuilder(args);

// ── Database Configuration ──
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SQLite";
var connString = dbProvider switch
{
    "SqlServer" => builder.Configuration.GetConnectionString("SqlServer"),
    "PostgreSQL" => builder.Configuration.GetConnectionString("PostgreSQL"),
    "MySql" => builder.Configuration.GetConnectionString("MySql"),
    _ => builder.Configuration.GetConnectionString("Default")
};

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer": options.UseSqlServer(connString); break;
        case "PostgreSQL": options.UseNpgsql(connString); break;
        case "MySql": options.UseMySql(connString, ServerVersion.AutoDetect(connString)); break;
        default: options.UseSqlite(connString); break;
    }
});

// ── Identity & Authentication ──
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// ── Blazor ──
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

// ── Storage Provider ──
builder.Services.AddStorageProvider(builder.Configuration);

// ── Application Services ──
builder.Services.AddScoped<KamarService>();
builder.Services.AddScoped<PenghuniService>();
builder.Services.AddScoped<TagihanService>();
builder.Services.AddScoped<PembayaranService>();
builder.Services.AddScoped<KomplainService>();
builder.Services.AddScoped<InventarisService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<KontrakService>();
builder.Services.AddScoped<KostService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<NotifikasiService>();
builder.Services.AddScoped<IoTService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<SeedService>();

// ── Semantic Kernel Chat ──
builder.Services.AddSingleton<ChatService>();

// ── Swagger ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "JuraganKost API", Version = "v1" }));

builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Database Seeding ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seeder.SeedAsync();
}

// ── Middleware ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// ── Auth Endpoints (Cookie Write Safe) ──
// NOTE:
// Gunakan path /submit agar tidak bentrok dengan endpoint Razor Component (/auth/login & /auth/register).
app.MapPost("/auth/login/submit", async (HttpContext http,
        SignInManager<ApplicationUser> signInManager) =>
    {
        var form = await http.Request.ReadFormAsync();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(returnUrl)) returnUrl = "/dashboard";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var err = WebUtility.UrlEncode("Email dan password wajib diisi.");
            return Results.Redirect($"/auth/login?error={err}&returnUrl={WebUtility.UrlEncode(returnUrl)}");
        }

        var result = await signInManager.PasswordSignInAsync(email, password, true, false);
        if (result.Succeeded)
        {
            return Results.Redirect(returnUrl);
        }

        var errorMsg = WebUtility.UrlEncode("Email atau password salah.");
        return Results.Redirect($"/auth/login?error={errorMsg}&returnUrl={WebUtility.UrlEncode(returnUrl)}");
    })
    .DisableAntiforgery();

app.MapPost("/auth/register/submit", async (HttpContext http,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager) =>
    {
        var form = await http.Request.ReadFormAsync();
        var nama = form["nama"].ToString();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var role = form["role"].ToString();

        var allowedRoles = new[] { "Penghuni", "Pemilik" };
        if (!allowedRoles.Contains(role)) role = "Penghuni";

        if (string.IsNullOrWhiteSpace(nama) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            var err = WebUtility.UrlEncode("Semua field wajib diisi.");
            return Results.Redirect($"/auth/register?error={err}");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            NamaLengkap = nama,
            RoleExt = Enum.Parse<UserRoleExt>(role),
            EmailConfirmed = true
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            var err = WebUtility.UrlEncode(string.Join(", ", create.Errors.Select(e => e.Description)));
            return Results.Redirect($"/auth/register?error={err}");
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            var err = WebUtility.UrlEncode(string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            return Results.Redirect($"/auth/register?error={err}");
        }

        await signInManager.SignInAsync(user, true);
        return Results.Redirect("/dashboard");
    })
    .DisableAntiforgery();

// ── API & Blazor ──
JuraganKost.Api.ApiEndpoints.Map(app);

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
