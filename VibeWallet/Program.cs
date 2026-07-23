using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VibeWallet.Data;
using VibeWallet.Models;
using VibeWallet.Services;
using VibeWallet.Api;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext()
    .WriteTo.Console().WriteTo.File("Logs/vibewallet-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<VibeWalletDbContext>((sp, options) =>
    VibeWalletDbContext.ConfigureDatabase(options, builder.Configuration));

builder.Services.AddIdentity<VibeUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true; options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true; options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedEmail = false; options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<VibeWalletDbContext>().AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true; options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.LoginPath = "/login"; options.SlidingExpiration = true;
});

builder.Services.Configure<VibeWalletConfig>(builder.Configuration.GetSection("VibeWallet"));
builder.Services.Configure<StorageConfig>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<ChatBotConfig>(builder.Configuration.GetSection("ChatBot"));
builder.Services.Configure<TransactionLimitsConfig>(builder.Configuration.GetSection("TransactionLimits"));
builder.Services.Configure<RewardsConfig>(builder.Configuration.GetSection("Rewards"));
builder.Services.Configure<KycConfig>(builder.Configuration.GetSection("KYC"));

// ===== Services =====
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ThemeService>(); // ← Sinkronisasi tema dark/light
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IRewardsService, RewardsService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IBankService, BankService>();
builder.Services.AddScoped<IKycService, KycService>();
builder.Services.AddScoped<IInvestmentService, InvestmentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHttpClient();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new() { Title = "VibeWallet API", Version = "v1" }); });
builder.Services.AddCors(options => { options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()); });

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
else { app.UseExceptionHandler("/Error"); app.UseHsts(); }

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

VibeWallet.Api.AuthEndpoints.MapAuthEndpoints(app);
VibeWallet.Api.Endpoints.MapAll(app);
app.MapRazorComponents<VibeWallet.Components.App>().AddInteractiveServerRenderMode();

try { await VibeWalletSeedData.InitializeAsync(app.Services); Log.Information("Seeded OK"); }
catch (Exception ex) { Log.Error(ex, "Seed error"); }

try
{
    var www = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(www, "uploads", "kyc"));
    Directory.CreateDirectory(Path.Combine(www, "uploads", "chat"));
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data"));
    Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Logs"));
}
catch { }

Log.Information("VibeWallet started");
app.Run();
