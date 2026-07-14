using BlazePoint.Api;
using BlazePoint.Components;
using BlazePoint.Data;
using BlazePoint.Services;
using BlazePoint.Services.Clippy;
using BlazePoint.Services.Search;
using BlazePoint.Services.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// ---------- Blazor ----------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("clippy", c =>
{
    c.Timeout = TimeSpan.FromSeconds(60);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("BlazePoint/1.0");
});

// ---------- Database (pluggable provider) ----------
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString(dbProvider)
    ?? throw new InvalidOperationException($"Connection string '{dbProvider}' tidak ditemukan.");

if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    // ensure the folder for the .db file exists
    var dataDir = System.IO.Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dataDir);
    connectionString = connectionString.Replace("App_Data/", dataDir + System.IO.Path.DirectorySeparatorChar);
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    switch (dbProvider.ToLowerInvariant())
    {
        case "sqlserver": options.UseSqlServer(connectionString); break;
        case "postgresql": options.UseNpgsql(connectionString); break;
        case "mysql": options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); break;
        default: options.UseSqlite(connectionString); break;
    }
});

// ---------- Identity / Auth ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
        options.SignIn.RequireConfirmedEmail = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();

// ---------- Storage (pluggable provider) ----------
var storageProvider = (builder.Configuration["Storage:Provider"] ?? "FileSystem").ToLowerInvariant();
builder.Services.AddSingleton<IFileStorage>(sp => storageProvider switch
{
    "azureblob" => new AzureBlobStorage(sp.GetRequiredService<IConfiguration>()),
    "s3" => new S3Storage(sp.GetRequiredService<IConfiguration>(), minio: false),
    "minio" => new S3Storage(sp.GetRequiredService<IConfiguration>(), minio: true),
    _ => new FileSystemStorage(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IWebHostEnvironment>())
});

// ---------- Search: embeddings + vector index ----------
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("clippy");
    return (cfg["Search:Embeddings:Provider"] ?? "Local").ToLowerInvariant() switch
    {
        "openai" => new OpenAIEmbeddingGenerator(http,
            cfg["Search:Embeddings:OpenAI:ApiKey"] ?? "",
            cfg["Search:Embeddings:OpenAI:Model"] ?? "text-embedding-3-small"),
        "ollama" => new OllamaEmbeddingGenerator(http,
            cfg["Search:Embeddings:Ollama:Endpoint"] ?? "http://localhost:11434",
            cfg["Search:Embeddings:Ollama:Model"] ?? "nomic-embed-text"),
        _ => new LocalHashEmbeddingGenerator()
    };
});
builder.Services.AddSingleton<IVectorIndex?>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("clippy");
    return (cfg["Search:VectorStore"] ?? "Local").ToLowerInvariant() switch
    {
        "qdrant" => new QdrantVectorIndex(http, cfg["Search:Qdrant:Endpoint"] ?? "http://localhost:6333",
            cfg["Search:Qdrant:Collection"] ?? "blazepoint"),
        "chroma" => new ChromaVectorIndex(http, cfg["Search:Chroma:Endpoint"] ?? "http://localhost:8000",
            cfg["Search:Chroma:Collection"] ?? "blazepoint"),
        _ => null // Local: cosine similarity in-database
    };
});

// ---------- Application services ----------
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<MarkdownService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<ListService>();
builder.Services.AddSingleton<PageService>();
builder.Services.AddSingleton<NavigationService>();
builder.Services.AddSingleton<SiteService>();
builder.Services.AddSingleton<WorkflowService>();
builder.Services.AddSingleton<CalendarService>();
builder.Services.AddSingleton<ShareLinkService>();
builder.Services.AddSingleton<DiscussionService>();
builder.Services.AddSingleton<FormService>();
builder.Services.AddSingleton<DashboardService>();
builder.Services.AddSingleton<ClippyService>();

// ---------- GraphQL ----------
builder.Services
    .AddGraphQLServer()
    .AddQueryType<GraphQLQuery>();

var app = builder.Build();

// ---------- Pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAccountEndpoints();
app.MapApiEndpoints();
app.MapGraphQL("/graphql");

// ---------- Seed ----------
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    // build the search index on first run
    var search = scope.ServiceProvider.GetRequiredService<SearchService>();
    var dbf = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await dbf.CreateDbContextAsync();
    if (!await db.SearchIndex.AnyAsync())
        await search.ReindexAllAsync();
}

app.Run();
