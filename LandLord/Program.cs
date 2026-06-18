using LandLord.Components;
using LandLord.Data;
using LandLord.Plugins;
using LandLord.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Konfigurasi Database ---
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider:Provider") ?? "SQLite";
var connectionString = builder.Configuration.GetValue<string>("DatabaseProvider:ConnectionString")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=LandLord.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SQLite": options.UseSqlite(connectionString); break;
        case "SQLServer": options.UseSqlServer(connectionString); break;
        case "MySQL": options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); break;
        case "PostgreSQL": options.UseNpgsql(connectionString); break;
        default: options.UseSqlite(connectionString); break;
    }
});

// --- Konfigurasi Storage Provider ---
var storageProvider = builder.Configuration.GetValue<string>("StorageProvider:Provider") ?? "FileSystem";
switch (storageProvider)
{
    case "AzureBlob":
        builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();
        Console.WriteLine("✅ Storage Provider: Azure Blob Storage"); break;
    case "S3":
        builder.Services.AddSingleton<IStorageService, S3StorageService>();
        Console.WriteLine("✅ Storage Provider: AWS S3"); break;
    case "MinIO":
        builder.Services.AddSingleton<IStorageService, S3StorageService>();
        Console.WriteLine("✅ Storage Provider: MinIO (S3-Compatible)"); break;
    case "FileSystem":
    default:
        builder.Services.AddSingleton<IStorageService, FileSystemStorageService>();
        Console.WriteLine("✅ Storage Provider: File System (Local)"); break;
}

// --- Kernel Functions Service (standalone, untuk backward compatibility) ---
builder.Services.AddSingleton<IKernelFunctionsService, KernelFunctionsService>();

// --- Register Services ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITanahService, TanahService>();
builder.Services.AddScoped<IBangunanService, BangunanService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

// ✅ CHAT SERVICE — Semantic Kernel dengan multi-LLM + Kernel Functions
builder.Services.AddScoped<IChatService, SkChatService>();

// --- Blazor & Authentication ---
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddAuthentication("LandLordAuth")
    .AddCookie("LandLordAuth", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// --- HttpClient ---
builder.Services.AddHttpClient();

var app = builder.Build();

// --- Inisialisasi Database & Seed Data ---
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(dbContext);
}

// --- Cek koneksi ---
using (var scope = app.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
    var storageOk = await storage.CheckConnectionAsync();
    Console.WriteLine(storageOk
        ? $"✅ Storage '{storage.ProviderName}' — Koneksi OK"
        : $"⚠️  Storage '{storage.ProviderName}' — Koneksi gagal");

    var tavilyApiKey = app.Configuration.GetValue<string>("Tavily:ApiKey");
    if (!string.IsNullOrEmpty(tavilyApiKey) && tavilyApiKey != "YOUR_TAVILY_API_KEY")
        Console.WriteLine("✅ Tavily Search API — API key terkonfigurasi");
    else
        Console.WriteLine("⚠️  Tavily Search API — API key belum dikonfigurasi");

    var llmProvider = app.Configuration.GetValue<string>("LLMProvider:Provider") ?? "OpenAI";
    var llmModel = app.Configuration.GetValue<string>("LLMProvider:Model") ?? "gpt-4o";
    var llmApiKey = app.Configuration.GetValue<string>("LLMProvider:ApiKey") ?? "";
    if (!string.IsNullOrEmpty(llmApiKey))
        Console.WriteLine($"✅ Semantic Kernel LLM: {llmProvider}/{llmModel}");
    else
        Console.WriteLine($"⚠️  Semantic Kernel LLM: {llmProvider}/{llmModel} — API key belum dikonfigurasi (mode fallback)");
}

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// --- Auth API Endpoints (Form POST) ---
app.MapPost("/api/auth/login", async (HttpContext http, IAuthService authService) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var redirect = form["redirect"].ToString();

    if (string.IsNullOrWhiteSpace(redirect))
        redirect = "/";

    var user = await authService.LoginAsync(username, password);
    return user is null
        ? Results.Redirect($"/login?error=1&redirect={Uri.EscapeDataString(redirect)}")
        : Results.Redirect(redirect);
})
.AllowAnonymous()
.DisableAntiforgery();

app.MapGet("/api/auth/logout", async (HttpContext http, IAuthService authService) =>
{
    await authService.LogoutAsync();

    var redirect = http.Request.Query["redirect"].ToString();
    if (string.IsNullOrWhiteSpace(redirect))
        redirect = "/login?loggedout=true";

    return Results.Redirect(redirect);
})
.AllowAnonymous();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
