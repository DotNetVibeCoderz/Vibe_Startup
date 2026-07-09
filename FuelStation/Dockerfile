# ============================================
# FUELSTATION - Dockerfile
# Multi-stage build untuk Blazor Server .NET 10
# ============================================

# ---- STAGE 1: Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project file dan restore dependencies
COPY FuelStation.csproj .
RUN dotnet restore FuelStation.csproj

# Copy seluruh source
COPY . .

# Build & publish
RUN dotnet publish FuelStation.csproj -c Release -o /app/publish --no-restore

# ---- STAGE 2: Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Install timezone data untuk WIB
ENV TZ=Asia/Jakarta
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Copy published output
COPY --from=build /app/publish .

# Create directories
RUN mkdir -p /app/uploads /app/receipts /app/wwwroot

# Expose port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Run
ENTRYPOINT ["dotnet", "FuelStation.dll"]
