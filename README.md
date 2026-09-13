# 🎧 Scrobblint

A lightweight, self-hosted scrobbling service — track what you listen to, your way. Think a small, easy-to-run ListenBrainz / Last.fm.

## What it does

- **Scrobbling** — submit listens with a personal API token, via the native API or a **ListenBrainz-compatible** endpoint (works with existing ListenBrainz clients).
- **Relaying** — forwards every listen onward to **Last.fm** and/or **ListenBrainz**, with automatic retries for failed relays.
- **Last.fm import** — pulls your full Last.fm history in a resumable background job.
- **Statistics** — top artists, albums and tracks; monthly, daily, hourly, weekday and yearly charts; a day × hour heatmap; date-range filters.
- **Browsing** — dashboard, searchable recent listens, artist / album / track pages with cover art, public or private user profiles.
- **Personal settings** — theme (light / dark / system), timezone, ignored scrobbles.
- **Administration** — user management, server status, logs, track cache and relay retry queue.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — or Docker / Podman
- SQLite (built in, default) or MySQL / MariaDB
- Optional: a Last.fm API key + secret to enable Last.fm relaying and import

## Tech stack

- **.NET 10**, ASP.NET Core Minimal APIs
- **Blazor** static server-side rendering — no JavaScript framework, plain CSS
- **Entity Framework Core** with SQLite and MySQL (Pomelo) providers
- Clean Architecture: Domain · Application · Infrastructure · Api · Web
- xUnit unit and integration tests

## Run it

```bash
dotnet run --project src/Web
# or
docker compose -f docker-compose.sqlite.yml up --build   # → http://localhost:8080
```

The database is created automatically and an admin account is seeded (**admin / ChangeMe!123**) — change it after first login.
