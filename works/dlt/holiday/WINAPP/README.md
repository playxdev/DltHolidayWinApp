# DLT Holiday WinApp

Windows desktop client (Windows Service) that synchronizes holiday data from the central DLT Holiday Admin API to on-premise SQL Server instances.

## Architecture

```
┌─────────────────────┐     HTTPS      ┌──────────────────┐
│  DLT Holiday Admin   │ ◄───────────── │   WinApp (.NET)   │
│  (Vercel / API)      │               │  10 on-premise    │
│  dlt-holiday-admin   │               │  Windows servers  │
│  .vercel.app         │               │  .NET 4.8 / C#    │
└─────────────────────┘               └──────────────────┘
```

- **API Server** — Vercel-hosted Next.js app dispatching holidays via REST
- **WinApp** — .NET Framework 4.8 Windows Service installed on each on-premise server
- **Schedule** — Runs daily at 01:00 AM (configurable via Windows Task Scheduler or built-in timer)
- **Payload** — Small JSON (~15 KB per call), read-only sync

## Concept

Each on-premise server:

1. Authenticates to the API (username + password + security token → JWT)
2. Fetches the active holiday list from `/api/holidays?status=1&pageSize=10000`
3. Compares against its local cache/database
4. Logs the sync result (local event log or log file)

The client is **read-only** — it does not create, update, or delete holidays.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Language | C# (.NET Framework 4.8) |
| HTTP Client | `System.Net.Http.HttpClient` |
| JSON | `System.Text.Json` or `Newtonsoft.Json` |
| Auth | JWT token via httpOnly cookie + Bearer header |
| Logging | Windows Event Log or NLog |
| Scheduling | Windows Task Scheduler or `System.Timers.Timer` |

## Requirements

- Windows 7+ / Windows Server 2008 R2+
- .NET Framework 4.8 runtime
- Outbound HTTPS access to `https://dlt-holiday-admin.vercel.app`
- Administrator privileges (for Windows Service installation)

## Quick Start

### 1. Configuration

Edit `App.config`:

```xml
<appSettings>
  <add key="ApiBaseUrl" value="https://dlt-holiday-admin.vercel.app" />
  <add key="AuthUsername" value="root" />
  <add key="AuthPassword" value="Root@@Kai200222" />
  <add key="AuthToken" value="@ETPNqq*9$GD+e9lyqrUa$M4#dG$^wtF" />
  <add key="FetchIntervalMinutes" value="1440" />  <!-- 24 hours -->
  <add key="FetchAtTime" value="01:00" />           <!-- 1:00 AM -->
</appSettings>
```

### 2. Build

```bash
# From Visual Studio or CLI
msbuild DltHolidayWinApp.csproj /p:Configuration=Release
```

### 3. Install as Windows Service

```bash
sc create "DLTHolidaySync" binPath="C:\Program Files\DltHoliday\DltHolidayWinApp.exe"
sc start DLTHolidaySync
```

### 4. Verify

Check Windows Event Log → Applications and Services Logs → DLT Holiday Sync for sync results.

## Workflow

```
01:00 AM ──► Timer fires
              │
              ├──► POST /api/auth/login  (get JWT cookie)
              │
              ├──► GET  /api/holidays?status=1&pageSize=10000
              │
              ├──► Compare with local database
              │       • New holidays    → INSERT
              │       • Updated holidays → UPDATE
              │       • Deactivated     → UPDATE active_status=0
              │
              ├──► Log results to Event Log
              │
              └──► Sleep until next interval
```

## Authentication Flow

```
POST /api/auth/login
Content-Type: application/json

{
  "username": "root",
  "password": "Root@@Kai200222",
  "token": "@ETPNqq*9$GD+e9lyqrUa$M4#dG$^wtF"
}

Response (200):
{
  "success": true,
  "token": "eyJhbGciOiJI..."
}

All subsequent requests include:
  Cookie: dlt_auth_token=<JWT>
```

The JWT token expires in 8 hours. Re-authenticate before each daily sync.

## API Reference

See [docs/API.md](docs/API.md) for the complete API reference.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/API.md](docs/API.md) | Complete API endpoint reference |
| [docs/workflow.md](docs/workflow.md) | Detailed sync workflow and data model |
| [docs/configuration.md](docs/configuration.md) | Configuration, scheduling, logging |

## License

Copyright © PlayDevX. All rights reserved.
