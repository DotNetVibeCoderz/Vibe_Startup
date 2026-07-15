# Deployment

## Development

```bash
dotnet run --project src/AppBender.Web
```

## Production build

```bash
dotnet publish src/AppBender.Web -c Release -o publish
ASPNETCORE_URLS=http://0.0.0.0:8080 dotnet publish/AppBender.Web.dll
```

Or containerize:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AppBender.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AppBender.Web.dll"]
```

## Database

The provider is configured in `appsettings.json` — no code changes needed:

```jsonc
"Database": { "Provider": "Sqlite" },   // Sqlite | SqlServer | Postgres | MySql
"ConnectionStrings": {
  "DefaultConnection": "DataSource=appbender.db;Cache=Shared"
}
```

Example connection strings:

| Provider | DefaultConnection |
|---|---|
| Sqlite (default) | `DataSource=appbender.db;Cache=Shared` |
| SqlServer | `Server=.;Database=AppBender;Trusted_Connection=True;TrustServerCertificate=True` |
| Postgres | `Host=localhost;Database=appbender;Username=postgres;Password=...` |
| MySql | `Server=localhost;Database=appbender;User=root;Password=...` |

The schema is created with `EnsureCreated()` on first startup against an empty database.
Record data is stored as JSON documents, so behavior is identical across providers.

## Storage

`Storage:Provider` selects where uploads/knowledge documents/exports live:

- `FileSystem` (default) — `storage/` next to the binary, served at `/files/*`
- `S3` — AWS S3 or **MinIO** (`ServiceUrl` for MinIO), config: keys + bucket
- `AzureBlob` — connection string + container

## Blazor Server scaling notes

- Single node handles hundreds of concurrent circuits comfortably; scale up before out.
- If you load-balance, enable **sticky sessions** (SignalR requirement) and a shared
  Data Protection key ring.
- Set `Email`, `AI`, and `Tavily` secrets via environment variables
  (`Section__Key` syntax, e.g. `AI__Providers__openai__ApiKey`).

## Background workers

Schedule and event triggers run inside the web process (hosted services) — keep at least one
always-on instance (disable IIS idle timeout / use `Always On`).

## Built-in advisor

**AI Studio → 🚢 Deployment Advisor** analyzes your actual usage (entity/record counts, workflow
mix) and produces tailored recommendations.
