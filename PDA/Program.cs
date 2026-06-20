using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PDA.Data;
using PDA.Models;
using PDA.Services;
using PDA.Services.Storage;
using PDA.Services.LLM;
using PDA.Services.LLM.KernelPlugins;
using PDA.Services.Database;
using PDA.Services.RAG;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=PDA.db";

// ============================================
// 1. Database
// ============================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString, sqliteOptions => sqliteOptions.MigrationsAssembly("PDA")));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString, sqliteOptions => sqliteOptions.MigrationsAssembly("PDA")), ServiceLifetime.Scoped);

// ============================================
// 2. Identity & Auth
// ============================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;
});

// ============================================
// 3. Blazor Server
// ============================================
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();

// ============================================
// 4. Storage Services (SEMUA SCOPED, bukan Singleton)
// ============================================
builder.Services.AddScoped<StorageServiceFactory>();
builder.Services.AddScoped<FileSystemStorageService>();
builder.Services.AddScoped<AzureBlobStorageService>();
builder.Services.AddScoped<S3StorageService>();
builder.Services.AddScoped<MinIOStorageService>();
builder.Services.AddScoped<IStorageService>(sp => sp.GetRequiredService<StorageServiceFactory>().Create());

// ============================================
// 5. Database Services
// ============================================
builder.Services.AddScoped<DatabaseConnectorFactory>();
builder.Services.AddScoped<SchemaExtractionService>();

// ============================================
// 6. Semantic Kernel & LLM Services
// ============================================
builder.Services.AddScoped<SemanticKernelFactory>();
builder.Services.AddScoped<ChatAgentService>();
builder.Services.AddScoped<DataAnalysisPlugin>();
builder.Services.AddScoped<KnowledgeBasePlugin>();
builder.Services.AddScoped<DashboardPlugin>();
builder.Services.AddScoped<CommonFunctionsPlugin>();
builder.Services.AddScoped<WebSearchPlugin>();

// ============================================
// 7. RAG, Audit, Monitoring
// ============================================
builder.Services.AddSingleton<RagIndexingService>();
builder.Services.AddHostedService<RagBackgroundWorker>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddSingleton<PdaMonitoringService>();
builder.Services.AddScoped<CommonFunctionsService>();

// ============================================
// 8. HTTP & Compression
// ============================================
builder.Services.AddHttpClient("DefaultClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "PDA/1.0");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// ============================================
// BUILD & SEED
// ============================================
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    try
    {
        await db.Database.EnsureCreatedAsync();
        await DataSeeder.SeedAsync(db, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database initialization error");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<PDA.Components.App>().AddInteractiveServerRenderMode();

app.MapPost("/auth/login", async (HttpContext httpContext, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    var form = await httpContext.Request.ReadFormAsync();
    var email = form["Email"].ToString();
    var password = form["Password"].ToString();
    var rememberMe = form["RememberMe"].ToString() is "on" or "true";
    var returnUrl = form["ReturnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return Results.Redirect("/login?error=invalid");

    var user = await userManager.FindByEmailAsync(email);
    if (user != null && !user.IsActive)
        return Results.Redirect("/login?error=inactive");

    var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
    if (result.Succeeded)
    {
        if (user != null) { user.LastLoginAt = DateTime.UtcNow; await userManager.UpdateAsync(user); }
        var safeUrl = (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/')) ? returnUrl : "/";
        return Results.Redirect(safeUrl);
    }
    return Results.Redirect(result.IsLockedOut ? "/login?error=locked" : "/login?error=invalid");
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext httpContext, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).AllowAnonymous();

app.MapPost("/api/chat/send", async (ChatRequest request, ChatAgentService chatAgent) =>
{
    try { return Results.Ok(await chatAgent.ProcessMessageAsync(request)); }
    catch (Exception ex) { return Results.Problem(ex.Message); }
}).RequireAuthorization();

app.MapPost("/api/chat/voice", () => Results.Ok(new { message = "🎤 Fitur Voice Chat belum di rilis. Coming soon!" })).RequireAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

await app.RunAsync();

public record ChatRequest
{
    public int SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MessageAttachment>? Attachments { get; set; }
}
