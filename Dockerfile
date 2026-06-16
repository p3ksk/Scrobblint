# syntax=docker/dockerfile:1

# ---- Build stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching): copy only project files.
COPY src/Domain/Scrobblint.Domain.csproj src/Domain/
COPY src/Shared/Scrobblint.Shared.csproj src/Shared/
COPY src/Application/Scrobblint.Application.csproj src/Application/
COPY src/Infrastructure/Scrobblint.Infrastructure.csproj src/Infrastructure/
COPY src/Api/Scrobblint.Api.csproj src/Api/
COPY src/Web/Scrobblint.Web.csproj src/Web/
COPY src/Migrations/Scrobblint.Migrations.Sqlite/Scrobblint.Migrations.Sqlite.csproj src/Migrations/Scrobblint.Migrations.Sqlite/
COPY src/Migrations/Scrobblint.Migrations.MySql/Scrobblint.Migrations.MySql.csproj src/Migrations/Scrobblint.Migrations.MySql/
RUN dotnet restore src/Web/Scrobblint.Web.csproj

# Copy the rest and publish the all-in-one Web host (UI + REST API).
COPY . .
RUN dotnet publish src/Web/Scrobblint.Web.csproj -c Release -o /app --no-restore

# ---- Runtime stage -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# SQLite database and data-protection keys live here; mount a volume to persist them.
RUN mkdir -p /data /data/keys
ENV Database__Provider=SQLite \
    Database__ConnectionString="Data Source=/data/scrobblint.db" \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "Scrobblint.Web.dll"]
