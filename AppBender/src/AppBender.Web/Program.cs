using AppBender.Core.AI;
using AppBender.Core.Connectors;
using AppBender.Core.Data;
using AppBender.Core.Services;
using AppBender.Core.Workflows;
using AppBender.Web.Components;
using AppBender.Web.Components.Account;
using AppBender.Web.Middleware;
using AppBender.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- UI
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// ---------------------------------------------------------------- auth
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Database provider is configurable: "Database:Provider" = Sqlite | SqlServer | Postgres | MySql,
// with the matching connection string under ConnectionStrings.
var dbProvider = (builder.Configuration["Database:Provider"] ?? "Sqlite").ToLowerInvariant();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "DataSource=appbender.db;Cache=Shared";
void ConfigureDb(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder options)
{
    switch (dbProvider)
    {
        case "sqlserver":
            options.UseSqlServer(connectionString);
            break;
        case "postgres" or "postgresql" or "npgsql":
            options.UseNpgsql(connectionString);
            break;
        case "mysql" or "mariadb":
            options.UseMySQL(connectionString);
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
}
builder.Services.AddDbContextFactory<ApplicationDbContext>(ConfigureDb);
// Identity needs a scoped DbContext; reuse the factory so both share one configuration.
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// ---------------------------------------------------------------- platform services
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IScriptingService, ScriptingService>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IUsageService, UsageService>();
builder.Services.AddScoped<IVersionService, VersionService>();
builder.Services.AddScoped<IDataHubService, DataHubService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IAppService, AppService>();
builder.Services.AddScoped<ISnippetService, SnippetService>();
builder.Services.AddScoped<GraphQlExecutor>();
builder.Services.AddScoped<IImportExportService, ImportExportService>();

// workflows
builder.Services.AddWorkflowActions();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddSingleton<WorkflowRunner>();
builder.Services.AddHostedService<ScheduleTriggerService>();
builder.Services.AddHostedService<EventTriggerService>();

// connectors
builder.Services.AddScoped<IConnector, RestApiConnector>();
builder.Services.AddScoped<IConnector, GraphQlConnector>();
builder.Services.AddScoped<IConnector, WebhookConnector>();
builder.Services.AddScoped<IConnector, SqlServerConnector>();
builder.Services.AddScoped<IConnector, PostgresConnector>();
builder.Services.AddScoped<IConnector, MySqlConnectorProvider>();
builder.Services.AddScoped<IConnector, SqliteConnector>();
builder.Services.AddScoped<IConnector, S3Connector>();
builder.Services.AddScoped<IConnector, AzureBlobConnector>();
builder.Services.AddScoped<IConnector, FileSystemConnector>();
builder.Services.AddScoped<IConnector, SmtpConnector>();
builder.Services.AddScoped<IConnector, TavilyConnector>();
builder.Services.AddScoped<IConnector, CustomConnector>();
builder.Services.AddScoped<IConnectorRuntime, ConnectorRuntime>();
builder.Services.AddScoped<IDataSyncService, DataSyncService>();

// AI
builder.Services.AddSingleton<IWebSearchClient, TavilyWebSearchClient>();
builder.Services.AddScoped<ILlmClient, SemanticKernelLlmClient>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IModelBuilderService, ModelBuilderService>();
builder.Services.AddScoped<IAiStudioService, AiStudioService>();

// ---------------------------------------------------------------- API + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AppBender Data API",
        Version = "v1",
        Description = "Auto-generated REST + GraphQL API over Data Hub entities. " +
                      "Authenticate with a session cookie or the X-Api-Key header."
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "API key configured under Api:Keys in appsettings.json"
    });
});

var app = builder.Build();

// ---------------------------------------------------------------- database + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

// ---------------------------------------------------------------- pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// serve uploaded/stored files (FileSystem storage provider) at /files
var storageRoot = builder.Configuration["Storage:FileSystem:BasePath"];
if (string.IsNullOrWhiteSpace(storageRoot))
    storageRoot = Path.Combine(AppContext.BaseDirectory, "storage");
Directory.CreateDirectory(storageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/files"
});

app.UseAuthentication();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<UsageTrackingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AppBender Data API v1");
    options.DocumentTitle = "AppBender API";
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
