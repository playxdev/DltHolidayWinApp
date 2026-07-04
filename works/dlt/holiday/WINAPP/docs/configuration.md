# WinApp Configuration Guide

## Config File: `App.config`

All settings are stored in the standard .NET `App.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- API endpoint -->
    <add key="ApiBaseUrl" value="https://dlt-holiday-admin.vercel.app" />

    <!-- Authentication -->
    <add key="AuthUsername" value="root" />
    <add key="AuthPassword" value="Root@@Kai200222" />
    <add key="AuthToken" value="@ETPNqq*9$GD+e9lyqrUa$M4#dG$^wtF" />

    <!-- Schedule (both must be set) -->
    <add key="FetchIntervalMinutes" value="1440" />
    <add key="FetchAtTime" value="01:00" />

    <!-- Retry settings -->
    <add key="RetryMaxAttempts" value="3" />
    <add key="RetryDelayMinutes" value="15" />

    <!-- Local SQL Server connection -->
    <add key="LocalDbConnectionString" value="Server=localhost;Database=DLT;Trusted_Connection=True;" />

    <!-- Logging -->
    <add key="EventLogSource" value="DLT Holiday Sync" />
    <add key="LogLevel" value="Information" />

    <!-- HTTP -->
    <add key="RequestTimeoutSeconds" value="30" />
  </appSettings>
</configuration>
```

## Settings Reference

| Key | Type | Required | Default | Description |
|-----|------|----------|---------|-------------|
| `ApiBaseUrl` | string | ✓ | — | DLT Holiday Admin API base URL (no trailing slash) |
| `AuthUsername` | string | ✓ | — | Login username |
| `AuthPassword` | string | ✓ | — | Login password |
| `AuthToken` | string | ✓ | — | Security token (3rd factor) |
| `FetchIntervalMinutes` | int | — | `1440` | Time between syncs in minutes |
| `FetchAtTime` | string | — | `01:00` | Specific time of day to sync (HH:mm, 24h) |
| `RetryMaxAttempts` | int | — | `3` | Max retries on network failure |
| `RetryDelayMinutes` | int | — | `15` | Minutes between retry attempts |
| `LocalDbConnectionString` | string | ✓ | — | SQL Server connection string for local DB |
| `EventLogSource` | string | — | `DLT Holiday Sync` | Windows Event Log source name |
| `LogLevel` | string | — | `Information` | Minimum log level (Debug, Information, Warning, Error) |
| `RequestTimeoutSeconds` | int | — | `30` | HTTP request timeout |

## Scheduling

Two scheduling strategies are supported:

### Option A: Built-in Timer (recommended for simplicity)

The app stays running as a Windows Service with an internal timer.

- Set `FetchIntervalMinutes` and/or `FetchAtTime` in `App.config`
- Timer checks every 60 seconds if the scheduled time matches
- No external scheduler needed

```csharp
// Example: timer check every 60 seconds
var timer = new Timer(_ =>
{
    var now = DateTime.Now;
    if (now.Hour == fetchHour && now.Minute == fetchMinute)
    {
        // Check that we haven't already synced this minute
        if (lastSyncTime.Date != now.Date || lastSyncTime.Hour != now.Hour || lastSyncTime.Minute != now.Minute)
        {
            SyncHolidays();
            lastSyncTime = now;
        }
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
```

### Option B: Windows Task Scheduler

- App runs as a console app (not service)
- Windows Task Scheduler triggers it at 01:00 AM daily

```xml
<!-- Task Scheduler XML fragment -->
<Schedule>
  <TimeTrigger>
    <StartBoundary>2026-07-04T01:00:00</StartBoundary>
    <Repetition>
      <Interval>PT24H</Interval>
    </Repetition>
  </TimeTrigger>
</Schedule>
```

## Deployment Checklist

Before installing on each server:

- [ ] .NET Framework 4.8 installed
- [ ] Outbound port 443 (HTTPS) open to `dlt-holiday-admin.vercel.app`
- [ ] `App.config` configured with correct credentials
- [ ] Local SQL Server accessible with given connection string
- [ ] Local `dbo.tb_holiday_server` table created
- [ ] Administrator rights for service installation
- [ ] Windows Event Log source registered:
  ```bash
  powershell New-EventLog -LogName Application -Source "DLT Holiday Sync"
  ```

## Service Installation

### Create the Event Log Source (run as Admin)

```powershell
# PowerShell (admin)
New-EventLog -LogName Application -Source "DLT Holiday Sync"
```

### Install as Windows Service

```cmd
:: Command Prompt (admin)
sc create "DLTHolidaySync" ^
  binPath="C:\Program Files\DltHoliday\DltHolidayWinApp.exe" ^
  start=auto ^
  DisplayName="DLT Holiday Sync Service"
```

### Start/Stop/Status

```cmd
sc start  DLTHolidaySync
sc stop   DLTHolidaySync
sc query  DLTHolidaySync
```

### Uninstall

```cmd
sc stop   DLTHolidaySync
sc delete DLTHolidaySync
```

## Logging

Logs are written to Windows Event Log (Application log, source `DLT Holiday Sync`).

View logs:

```cmd
wevtutil qe Application /c:20 /f:text /q:"*[System[Provider[@Name='DLT Holiday Sync']]]"
```

Or via Event Viewer:
1. Open Event Viewer (`eventvwr.msc`)
2. Expand Windows Logs → Application
3. Filter Current Log → Event sources → `DLT Holiday Sync`

### Log Levels

| Level | When |
|-------|------|
| `Information` | Normal sync completion with summary |
| `Warning` | Retry attempts, partial failures |
| `Error` | Auth failures, DB connection errors, full sync failure |
| `Debug` | Per-request details, raw JSON payloads |

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| 401 Unauthorized | Wrong credentials or expired JWT | Re-check `AuthUsername`, `AuthPassword`, `AuthToken` in `App.config` |
| Connection refused | Firewall blocking outbound 443 | Allow HTTPS to `dlt-holiday-admin.vercel.app` |
| DB connection failure | Local SQL Server not running | Check service `MSSQLSERVER`, verify connection string |
| Timeout | Network too slow | Increase `RequestTimeoutSeconds` |
| Service won't start | Missing Event Log source | Run `New-EventLog` PowerShell command |
| No logs | Log source not registered | Register source with `New-EventLog` |
