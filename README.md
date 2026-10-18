# HomeDaemon

Modular home automation backend for the **Chores** module, built as a .NET 10 modular
monolith hosted in a single LXC container. It keeps Home Assistant to-do lists in sync with
a deterministic, calendar-aware scheduler and records completion history.

This repository implements the technical plan in
[`analysis/technical_analysis.md`](analysis/technical_analysis.md) (Italian). The code,
identifiers, tests and comments are in English.

## Architecture

```text
HomeDaemon.Api          ASP.NET Core host: endpoints, auth, workers, composition root
HomeDaemon.Application  CQRS handlers, recurrence, scheduler, availability, sync planning
HomeDaemon.DataAccess   EF Core + SQLite, migrations, Home Assistant HTTP adapter, backup
HomeDaemon.Domain       Entities, enums and value types; no external dependencies
```

Direct project references:

- `Api → Application`, `Api → DataAccess` (DataAccess only in the composition root).
- `Application → Domain` (plus EF Core abstractions for `DbSet`/`IQueryable`/transactions).
- `DataAccess → Application`, `DataAccess → Domain`.
- `Domain → nothing`.

The architecture tests in `tests/HomeDaemon.ArchitectureTests` enforce these rules.

### Request flows

- **Home Assistant → HomeDaemon:** `POST /api/v1/integrations/home-assistant/todo-completions`
  records a completion idempotently, advances the theoretical occurrence and re-places the
  chore.
- **HomeDaemon → Home Assistant:** plans are written to the `todo_sync_outbox` in the same
  transaction as the plan, then dispatched by a background worker. The database and Home
  Assistant never share a transaction.
- **Scheduler:** the nightly job, the manual endpoint and any future trigger all call the
  same `RecalculateScheduleCommandHandler` → `RecalculationExecutor` case.

## The three dates

| Concept | Property | Rewritten by the scheduler? |
|---|---|---|
| Last completion | `ChoreDefinition.LastCompletedDate` | No |
| Theoretical next occurrence | `ChoreDefinition.NextOccurrenceDate` | No |
| Scheduled next due date | `ChoreDefinition.NextDueDate` | Yes |

Domain dates are `DateOnly`; technical timestamps are UTC `DateTimeOffset`. `DateTimeOffset`
is stored as sortable ticks so SQLite can order and range-compare it.

## Recurrence and availability

- Frequencies: daily, weekly, monthly, yearly and fixed day interval, in `Dynamic`
  (roll-on-completion) or `Anchored` mode. Monthly/yearly recurrences clamp to the last valid
  day of the target month (for example 31 January → 28/29 February).
- Availability is computed in the household's local window (default 08:00–21:00). Only the
  overlap of an event with that window is subtracted, overlapping events are merged, all-day
  events block the day and multi-day events are split per day.
- The scheduler is pure and deterministic: hard constraints first, then a documented score
  (`SchedulingOptions`) with stable tie-breakers.

## Running locally

```bash
# restore + build + test
dotnet build
dotnet test --project tests/HomeDaemon.UnitTests/HomeDaemon.UnitTests.csproj
dotnet test --project tests/HomeDaemon.IntegrationTests/HomeDaemon.IntegrationTests.csproj
dotnet test --project tests/HomeDaemon.ArchitectureTests/HomeDaemon.ArchitectureTests.csproj

# run the API (Development applies migrations on startup)
dotnet run --project src/HomeDaemon.Api
```

`dotnet test` uses the new Microsoft.Testing.Platform runner, configured in `global.json`.

### Configuration

Non-secret settings live in `src/HomeDaemon.Api/appsettings.json`; secrets are supplied via
environment variables or container secrets and are never committed.

| Section | Key | Purpose |
|---|---|---|
| `ConnectionStrings` | `HomeDaemon` | SQLite file path |
| `Database` | `ApplyMigrationsOnStartup` | Apply migrations when the single instance starts |
| `ApiKey` | `Value` | Machine credential for Home Assistant → HomeDaemon (`X-Api-Key`) |
| `HomeAssistant` | `BaseUrl`, `Token`, `TimeoutSeconds` | Outbound Home Assistant calls |
| `Recalculation` | `NightlyRunTime` | Local time of the nightly recalculation |
| `Backup` | `Enabled`, `DailyRunTime`, `Directory`, `RetentionCount` | Daily SQLite backup |
| `TodoSync` | `BatchSize`, `MaxAttempts`, `SyncIntervalSeconds` | Outbox dispatch |
| `Scheduling` | weights | Soft scoring weights |

Set secrets like this:

```bash
export ApiKey__Value="<generate a long random value>"
export HomeAssistant__Token="<long-lived access token>"
export HomeAssistant__BaseUrl="http://homeassistant.local:8123/"
```

### Migrations

```bash
dotnet tool install --global dotnet-ef --version 10.0.13
dotnet-ef migrations add <Name> --project src/HomeDaemon.DataAccess --startup-project src/HomeDaemon.DataAccess --output-dir Persistence/Migrations
```

EF migrations are generated code; analyzer/style enforcement is disabled for them via
`.editorconfig`.

## Continuous delivery and container image

Pushing a **release tag** whose commit is on `main` runs
[`.github/workflows/release.yml`](.github/workflows/release.yml):

1. **Build and test** — the tagged commit must be an ancestor of `main`; the solution is
   restored, built in `Release` and all three test projects run.
2. **Publish container image** — [`Dockerfile`](Dockerfile) is built for `linux/amd64` and
   `linux/arm64` and pushed to GitHub Container Registry (GHCR) as
   `ghcr.io/geo-exe/home-daemon`, tagged with the full version, `major.minor`, `major`,
   `latest` (non-prerelease tags only) and `sha-<short>`.
3. **Create GitHub release** — a release with auto-generated notes.

```bash
git switch main
git pull
git tag -a v1.0.0 -m "HomeDaemon 1.0.0"
git push origin v1.0.0
```

### Secrets

The workflow requires **no repository or environment secret**. It authenticates to GHCR
with the ephemeral `GITHUB_TOKEN` that Actions issues per run, and each job only requests
the permissions it needs (`contents: read`, `packages: write`). Application secrets
(`ApiKey__Value`, `HomeAssistant__Token`, …) are never committed and never baked into the
image: they are injected at run time from environment variables on the host.

The repository being public does **not** make the package public. After the first publish,
open **GitHub → profile → Packages → home-daemon → Package settings → Danger Zone → Change
visibility → Public** so hosts can pull without logging in. If you keep it private, run
`docker login ghcr.io` on the host and give Watchtower the same credentials
(`~/.docker/config.json` or `WATCHTOWER_REGISTRY_AUTH`).

## Running the container

[`Dockerfile`](Dockerfile) builds the API on the .NET 10 SDK image and runs it on the
ASP.NET Core runtime as the non-root `app` user (UID 1654). Configuration comes entirely
from environment variables, and state lives in two volumes: `/data` (SQLite database) and
`/backups`.

```bash
docker build -t home-daemon:local .
docker run --rm -p 8080:8080 \
  -e ApiKey__Value="<long random value>" \
  -e HomeAssistant__BaseUrl="http://homeassistant.local:8123/" \
  -e HomeAssistant__Token="<token>" \
  -v home-daemon-data:/data \
  home-daemon:local
```

### Deploying on an LXC container with automatic updates

[`docker-compose.yml`](docker-compose.yml) runs the API next to a
[Watchtower](https://github.com/nicholas-fedor/watchtower) container that polls GHCR and
recreates the API when a new `latest` image appears.

```bash
# On the LXC container (Debian/Ubuntu): install Docker once
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

# Fetch this repository for the compose file
git clone https://github.com/Geo-exe/home-daemon.git
cd home-daemon

# Create the local environment file and fill in the secrets
cp .env.example .env
chmod 600 .env
$EDITOR .env

# Start the API and the updater
docker compose up -d
```

Watchtower checks for a new image every hour by default; change it with
`WATCHTOWER_POLL_INTERVAL` (seconds) in `.env`. To update immediately:

```bash
docker compose pull home-daemon
docker compose up -d home-daemon
```

Inspect the deployment:

```bash
docker compose ps
docker compose logs -f home-daemon
curl -fsS http://127.0.0.1:8080/health/ready
```

The API listens on `127.0.0.1:8080` only. Expose it through a reverse proxy, VPN or
firewall and terminate TLS at the boundary.

#### Environment variables

The compose file maps the essentials (`ConnectionStrings__HomeDaemon`,
`Database__ApplyMigrationsOnStartup`, `Backup__*`) and reads the rest from `.env`. The full
list of keys is in [Configuration](#configuration) above; the values are identical, only
the `:` separator becomes `__`.

#### Persisting on host paths instead of volumes

Named volumes inherit the image's ownership, so the non-root `app` user can write them
without extra steps. For bind mounts, create the directories and give them to UID 1654
first:

```bash
mkdir -p ./data ./backups
sudo chown -R 1654:1654 ./data ./backups
```

then replace the `volumes:` entries in `docker-compose.yml` with `./data:/data` and
`./backups:/backups`.

## Authentication

- **Home Assistant** uses a dedicated machine credential sent in the `X-Api-Key` header
  (never in the URL), validated with a constant-time comparison.
- **People** sign in to the web app with an ASP.NET Core Identity account. The session is an
  `HttpOnly`, `SameSite` cookie (`Secure` outside Development); no token is ever exposed to
  JavaScript.
- Mutating requests authenticated by cookie must carry the antiforgery token in
  `X-CSRF-TOKEN`. `GET /api/v1/auth/csrf` issues the token together with its cookie; requests
  authenticated by the Home Assistant API key are exempt.
- Accounts carry the `Admin` or `User` role. Administration lives under
  `/api/v1/admin/users` and requires the administrator role. Accounts are deactivated rather
  than deleted, so completion history stays valid.
- An account can be linked to one household member. A manual completion is attributed to the
  signed-in account and, when linked, to that member; the client cannot claim a different
  person. Home Assistant callbacks stay unattributed unless the payload names a member.
- A newly created or reset account must change its password before the rest of the API becomes
  available. The rule is enforced server-side: the API answers `403` until the change succeeds.
- A fallback authorization policy requires an authenticated principal for every endpoint;
  only the health probes, the OpenAPI document and the sign-in endpoints are anonymous.

### Creating the first administrator

Registration is deliberately not exposed over HTTP. Create the first account with a one-off
command, which also applies pending migrations:

```bash
export HOMEDAEMON_ADMIN_EMAIL="you@example.com"
export HOMEDAEMON_ADMIN_PASSWORD="a-long-temporary-password"
dotnet run --project src/HomeDaemon.Api -- create-admin
```

The command does nothing if an administrator already exists. The password must be changed at
the first sign-in. On the container:

```bash
docker compose run --rm \
  -e HOMEDAEMON_ADMIN_EMAIL="you@example.com" \
  -e HOMEDAEMON_ADMIN_PASSWORD="a-long-temporary-password" \
  home-daemon create-admin
```

Sign in with `POST /api/v1/auth/login`, and see the current account at `GET /api/v1/auth/me`.

## Health checks

- `GET /health/live` — process liveness.
- `GET /health/ready` and `GET /health` — readiness including a SQLite check. A Home
  Assistant outage does not make the API unavailable; failures surface through scheduling
  diagnostics and the outbox.

## Deployment notes (LXC)

- Deploy the published image (see [Running the container](#running-the-container)) and run it
  as the non-root `app` user.
- Mount a persistent volume for the SQLite file and keep the backup directory **outside** the
  database volume.
- Provide connection string, timezone, job times, planning window, Home Assistant URL and
  tokens as environment variables; the image contains none of them.
- Do not expose ports directly; use firewall/reverse proxy/VPN and TLS at the boundary.
- One application instance only: SQLite serializes writes. A WAL journal and busy timeout are
  applied automatically on connection open.
