# 🎧 Scrobblint

A lightweight, self-hosted **scrobbling service** built with **.NET 10**, **ASP.NET Core Minimal APIs**, **Entity Framework Core** and **Blazor** (server-side rendering). It is conceptually similar to ListenBrainz / Last.fm but deliberately small and easy to run.

- Register, then authenticate machine clients with a personal **API token** (`Authorization: Token …`) — no OAuth, no external identity providers.
- Submit scrobbles individually or in batches.
- Browse recent listens, a statistics dashboard (top artists/albums/tracks, monthly & daily charts) and public user profiles.
- Manage your token and profile settings; administrators can manage users.
- Provider-agnostic storage: **SQLite** (default) and **MySQL** today, with a clean path to PostgreSQL / SQL Server / MongoDB.

---

## Table of contents

- [Architecture](#architecture)
- [Project structure](#project-structure)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [REST API](#rest-api)
- [Web pages](#web-pages)
- [Database & migrations](#database--migrations)
- [Docker](#docker)
- [Testing](#testing)
- [Security notes](#security-notes)

---

## Architecture

Scrobblint follows Clean Architecture: dependencies point inward, the core has no knowledge of the database engine or the web framework.

```
Domain         ← entities + enums (no dependencies)
Application    ← service interfaces & logic, repository/security abstractions, Result type
Infrastructure ← EF Core DbContext, repositories, storage providers, password hashing, seeding
Api            ← Minimal API endpoints, token auth handler, rate limiting, Swagger (reusable module)
Web            ← Blazor (static SSR) UI with cookie auth; also hosts the REST API in-process
Shared         ← request/response DTOs shared by Api, Web and tests
```

Two deployable hosts share the same core:

| Host | Purpose | Auth |
|------|---------|------|
| **Web** (`src/Web`) | The all-in-one deployment — Blazor UI **and** the REST API in one process | Cookies (UI) + Token (API) |
| **Api** (`src/Api`) | REST API only, with Swagger UI | Token |

The provider-agnostic storage abstraction is `IDataStorageProvider` (in `Application`); the EF-aware specialisation `IEfDataStorageProvider` and the concrete `SqliteDataStorageProvider` / `MySqlDataStorageProvider` live in `Infrastructure`. `DataStorageProviderFactory` selects one from configuration.

### Notable design choices

- **No MediatR / AutoMapper / FluentValidation.** Mapping and validation are hand-written (`Mappers`, `ValidationBuilder`).
- **No ASP.NET Identity.** Custom user management with the framework's `PasswordHasher` (PBKDF2) wrapped behind `IPasswordHasher`.
- **`Result` / `Result<T>`** carry expected failures (validation, not-found, conflict, forbidden) instead of exceptions, and map cleanly to HTTP status codes and UI messages.
- **Timestamps are stored as UTC `DateTime`** so ordering/grouping translates across every provider (SQLite cannot `ORDER BY` a `DateTimeOffset`). DTOs expose Unix-seconds.
- **Statistics aggregate in the database** (grouped/paged queries), formatted in memory.
- **Multi-provider migrations** live in dedicated assemblies (`Scrobblint.Migrations.Sqlite` / `…MySql`) — the documented EF approach for supporting more than one engine.

---

## Project structure

```
src/
 ├─ Domain/            User, Scrobble, UserSettings + enums
 ├─ Application/       Services, abstractions, Result, validation, mapping
 ├─ Infrastructure/    DbContext, repositories, providers, security, seeding
 ├─ Api/              Minimal API endpoints, token auth, rate limiting, Swagger
 ├─ Web/              Blazor SSR UI + cookie auth + account/admin form endpoints
 ├─ Shared/           DTOs / contracts
 └─ Migrations/
     ├─ Scrobblint.Migrations.Sqlite/
     └─ Scrobblint.Migrations.MySql/
tests/
 ├─ Scrobblint.UnitTests/         service tests against in-memory SQLite
 └─ Scrobblint.IntegrationTests/  API tests via WebApplicationFactory
```

---

## Quick start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# Run the full app (UI + API). Uses SQLite by default; the database is
# created and migrated automatically, and an admin account is seeded.
dotnet run --project src/Web
```

Then open the printed URL (e.g. `http://localhost:5269`). Default seeded admin: **admin / ChangeMe!123** — change it after first login.

Run the **API-only** host (with Swagger UI at `/swagger` in Development):

```bash
dotnet run --project src/Api
```

Build everything / run tests:

```bash
dotnet build
dotnet test
```

---

## Configuration

Configuration is read from `appsettings.json`, environment variables (`Section__Key`) and the usual ASP.NET Core sources.

```jsonc
{
  "Database": {
    "Provider": "SQLite",                       // or "MySQL"
    "ConnectionString": "Data Source=scrobblint.db",
    "ServerVersion": "8.4.0",                   // MySQL/MariaDB only (optional)
    "ApplyMigrationsOnStartup": true
  },
  "Seed": {
    "Admin": {
      "Enabled": true,                          // seed an admin if none exists
      "Username": "admin",
      "Email": "admin@example.com",
      "Password": "ChangeMe!123"
    }
  }
}
```

MySQL example connection string:

```
Server=localhost;Port=3306;Database=scrobblint;User=scrobblint;Password=scrobblintpass;
```

As environment variables (e.g. for containers):

```
Database__Provider=MySQL
Database__ConnectionString=Server=db;Port=3306;Database=scrobblint;User=scrobblint;Password=scrobblintpass;
Seed__Admin__Enabled=true
```

---

## REST API

All API routes are prefixed with `/api`. Authenticated routes expect the ListenBrainz-style header:

```
Authorization: Token YOUR_TOKEN
```

### Authentication

| Method & path | Auth | Body | Response |
|---|---|---|---|
| `POST /api/auth/register` | — | `{ "username", "email", "password" }` | `201` `{ id, username, email, token }` |
| `POST /api/auth/login` | — | `{ "usernameOrEmail", "password" }` | `200` `{ token }` |
| `POST /api/auth/token` | Token | — | `200` `{ token }` (regenerates) |

### Scrobbles

| Method & path | Auth | Body |
|---|---|---|
| `POST /api/scrobble` | Token | `{ "artist", "track", "album?", "timestamp?" }` |
| `POST /api/scrobbles` | Token | `{ "scrobbles": [ { … }, … ] }` |

`timestamp` is Unix seconds; if omitted the server stamps "now". Both endpoints return `{ "accepted": N }`.

### Users (public, respect profile visibility)

| Method & path | Description |
|---|---|
| `GET /api/user/{username}` | Public profile + latest scrobble |
| `GET /api/user/{username}/recent?page=&pageSize=` | Paged recent listens |
| `GET /api/user/{username}/stats` | Totals, top artists/albums/tracks, monthly & daily charts |

### Admin (require admin token)

| Method & path | Description |
|---|---|
| `GET /api/admin/users?page=&pageSize=&search=` | List users with scrobble counts |
| `GET /api/admin/users/{id}` | User detail |
| `POST /api/admin/users/{id}/disable` · `/enable` | Disable / enable an account |
| `POST /api/admin/users/{id}/token` | Regenerate a user's token |

### Examples

```bash
# Register
curl -X POST http://localhost:5269/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@example.com","password":"supersecret"}'

# Submit a scrobble
curl -X POST http://localhost:5269/api/scrobble \
  -H "Authorization: Token YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"artist":"Radiohead","track":"Idioteque","album":"Kid A"}'

# Recent listens
curl http://localhost:5269/api/user/alice/recent
```

---

## Web pages

| Public | Authenticated | Admin |
|---|---|---|
| `/` Home | `/dashboard` | `/admin/users` |
| `/register` | `/recent` | `/admin/users/{id}` (disable/enable, regenerate token) |
| `/login` | `/stats` | |
| `/user/{username}` profile | `/token` (view / regenerate) | |
| | `/settings` (visibility, theme) | |

The UI is rendered with **static server-side rendering** (minimal JavaScript). Forms post to small endpoints that validate the **antiforgery** token and write the auth cookie.

---

## Database & migrations

Migrations are applied automatically on start-up (`Database:ApplyMigrationsOnStartup`). Each provider keeps its own migration set in its own assembly.

To add a migration after changing the model (the `dotnet-ef` tool is pinned in `dotnet-tools.json`):

```bash
dotnet tool restore

# SQLite
dotnet dotnet-ef migrations add <Name> \
  --project src/Migrations/Scrobblint.Migrations.Sqlite \
  --startup-project src/Migrations/Scrobblint.Migrations.Sqlite \
  --context ScrobblintDbContext --output-dir Migrations

# MySQL
dotnet dotnet-ef migrations add <Name> \
  --project src/Migrations/Scrobblint.Migrations.MySql \
  --startup-project src/Migrations/Scrobblint.Migrations.MySql \
  --context ScrobblintDbContext --output-dir Migrations
```

**Adding a new provider** (e.g. PostgreSQL): implement `IEfDataStorageProvider`, add a `case` to `DataStorageProviderFactory`, create a `Scrobblint.Migrations.Postgres` assembly, and reference it from the hosts.

---

## Docker

Single container with SQLite (data persisted in a named volume):

```bash
docker compose -f docker-compose.sqlite.yml up --build
# → http://localhost:8080
```

App + MySQL:

```bash
docker compose -f docker-compose.mysql.yml up --build
# → http://localhost:8080  (waits for MySQL to be healthy)
```

Both seed an admin (`admin` / `ChangeMe!123`) — override the `Seed__Admin__*` variables in the compose file.

---

## Testing

```bash
dotnet test
```

- **Unit tests** exercise the services against a real in-memory SQLite database with the real repositories (so EF query translation is verified, not mocked).
- **Integration tests** spin up the API with `WebApplicationFactory`, applying migrations and seeding against a throwaway SQLite file, and cover registration, token auth, scrobbling, stats, admin authorization and rate-limit-free happy paths.

---

## Security notes

- Passwords hashed with ASP.NET Core's PBKDF2 `PasswordHasher` (behind `IPasswordHasher`).
- API tokens are 256-bit, URL-safe, generated with a cryptographic RNG; regenerating one immediately invalidates the previous token.
- Disabled accounts cannot log in, scrobble, or be viewed.
- **Rate limiting** via the built-in limiter (a generous global window keyed by token/IP, plus a stricter limiter on login/register).
- **CSRF**: all UI form posts carry and validate an antiforgery token.
- **Authorization policies**: the REST API admin surface is pinned to the token scheme + admin role; the UI uses cookie auth with the same admin role.
- Input is validated server-side in every service; profile visibility is enforced for recent/stats/profile reads.

> Run behind a TLS-terminating reverse proxy in production, and change the seeded admin password immediately.
