# Holiday Sync Engine — .NET Framework 4.8 Console App

A one-shot console application that syncs holiday data from the Vercel API to a local MSSQL database. Deployed on each on-premise server, triggered by Windows Task Scheduler.

## Quick Start

1. **Build** with Visual Studio 2019+ or MSBuild (targeting .NET Framework 4.8)
2. **Place `curl.exe`** next to `HolidaySync.exe` (download from https://curl.se/windows/)
3. **Configure `edriving.ini`** with your server, database, and API credentials
4. **Run** directly or schedule via Task Scheduler

## Why curl.exe?

On Windows Server 2012 R2, TLS 1.2 is disabled by default in SCHANNEL (the OS TLS provider). .NET Framework's `HttpClient` / `HttpWebRequest` both route through SCHANNEL and will fail with *"Could not create SSL/TLS secure channel"*.

`curl.exe` bundles its own TLS stack (OpenSSL/Schannel), bypassing .NET's dependency on the OS TLS configuration. Same binary works on WS2012 R2, WS2016, WS2019, and Windows 10/11 without any registry changes.

The app auto-detects `curl.exe` in this order:
1. `curl.exe` next to `HolidaySync.exe`
2. `curl.exe` on system `PATH`

## Project Structure

```
HolidaySync/
├── HolidaySync.csproj          # SDK-style .NET 4.8 project
├── App.config                  # Runtime binding redirects + TLS AppContext switches
├── edriving.ini                # Configuration (server, database, API auth)
├── Program.cs                  # Entry point
├── .gitignore
├── docs/
│   └── GITHUB.md               # Git workflow & rules
├── Models/
│   └── Holiday.cs              # DTO: HolidayDto, PaginatedApiResponse<T>, LoginResponse
└── Helpers/
    ├── IniParser.cs             # INI file reader (sections, comments, quoted values)
    ├── TopLineLogger.cs         # Thread-safe logger (monthly rotation, colored console)
    ├── CurlHelper.cs            # HTTP via curl.exe (HEAD, GET, POST JSON)
    └── SyncEngine.cs            # Main sync logic
```

## Log Format

Log files follow the naming pattern `sync_log-{SERVER_NAME}-{yyyy-MM}.log` and write newest entry first. The `{SERVER_NAME}` is read from `[SERVER_INFO]` in `edriving.ini`.

When the month rolls over, a new log file is created automatically — no manual rotation needed.

Example filenames:

```
sync_log-holiday-2026-07.log
sync_log-holidaycloud-2026-08.log
sync_log-DLT-01-2026-07.log
```

```
[2026-07-04 12:51:54]🟢 SUCCESS > Loaded INI (DLT-01)
[2026-07-04 12:51:54]🔵 INFO > Sync started for 'DLT-01'
[2026-07-04 12:51:54]🟢 SUCCESS > Connected Vercel
[2026-07-04 12:51:54]🟢 SUCCESS > Connected Local Database
[2026-07-04 12:51:54]🟢 SUCCESS > Logged in to API
[2026-07-04 12:51:55]🔵 INFO > Fetched page 1/1 (150 records)
[2026-07-04 12:51:55]✅ DONE > UPDATE table on 'DLT-01' (150 records)
```

| Level | Emoji | Console Color | Meaning |
|-------|-------|--------------|---------|
| INFO | 🔵 | Default | Progress step |
| SUCCESS | 🟢 | Green | Check passed |
| FATAL | 🔴 | Red | Critical failure, app exits |
| WARNING | 🟡 | Yellow | Non-fatal issue |
| DONE | ✅ | Green | Sync completed |
| DEBUG | 🐛 | (file only) | Stack traces, dev details |

## Sync Flow

1. **Load INI** — read `[DATABASE]`, `[SERVER_INFO]`, `[API_AUTH]`
2. **Pre-flight HTTP** — HEAD to Vercel base URL
3. **Pre-flight DB** — `SELECT 1` on local SQL Server
4. **Login** — POST `/api/auth/login` → JWT token
5. **Download** — GET `/api/holidays` (paginated, 250/page)
6. **Atomic overwrite** — `DELETE FROM tb_holiday` + `SqlBulkCopy` in a transaction
7. **Wait 5s** — show result before window closes

## Scheduling

Recommended: Windows Task Scheduler. Trigger daily at 01:00 AM.

```cmd
schtasks /create /tn "DLT Holiday Sync" /tr "C:\DltHoliday\HolidaySync.exe" /sc daily /st 01:00 /ru SYSTEM
```
