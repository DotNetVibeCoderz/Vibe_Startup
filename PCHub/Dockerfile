# ===== PCHub Dockerfile =====
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY PCHub.sln ./
COPY src/PCHub.Shared/PCHub.Shared.csproj src/PCHub.Shared/
COPY src/PCHub.Admin/PCHubAdmin.csproj src/PCHub.Admin/
COPY src/PCHub.Client/PCHub.Client.csproj src/PCHub.Client/

# Restore dependencies
RUN dotnet restore src/PCHub.Admin/PCHubAdmin.csproj

# Copy everything else
COPY src/PCHub.Shared/ src/PCHub.Shared/
COPY src/PCHub.Admin/ src/PCHub.Admin/

# Build & Publish Admin
WORKDIR /src/src/PCHub.Admin
RUN dotnet publish PCHubAdmin.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -u 1000 pchub && mkdir -p /app/uploads /app/data && chown -R pchub:pchub /app
USER pchub

COPY --from=build /app/publish ./

EXPOSE 5001
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=SQLite
ENV Database__SQLite="Data Source=/app/data/PCHub.db"

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5001/ || exit 1

ENTRYPOINT ["dotnet", "PCHubAdmin.dll"]
