# WinApp Workflow & Data Model

## Overview

The WinApp runs as a Windows Service on each on-premise server. Once per day at 01:00 AM, it syncs the latest holiday data from the DLT Holiday Admin API to the local SQL Server.

## Sync Workflow (Step by Step)

```
┌──────────────────────────────────────────────────────────┐
│                      01:00 AM                            │
│                    Timer triggers                         │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 1: Health Check                                    │
│  GET /api/health                                         │
│  Confirm API + DB are online before proceeding.           │
│  If offline → log warning, retry in 15 minutes (× 3)     │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 2: Authenticate                                    │
│  POST /api/auth/login                                    │
│  Obtain JWT session cookie (dlt_auth_token).              │
│  Store cookie in HttpClientHandler.CookieContainer.       │
│  If 401 → log error, abort sync, notify admin.            │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 3: Fetch Active Holidays                           │
│  GET /api/holidays?status=1&pageSize=10000                │
│  Returns all active holidays in one page.                 │
│  Parse JSON → List<Holiday>.                              │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 4: Fetch Inactive Holidays (recent)                │
│  GET /api/holidays?status=0&pageSize=10000               │
│  Get inactive holidays to deactivate locally too.         │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 5: Compare & Sync to Local DB                      │
│                                                          │
│  For each holiday from API:                              │
│                                                          │
│  ┌─ EXISTS in local DB (by holiday_id)?                  │
│  │  ├─ YES: Fields different? → UPDATE local row          │
│  │  └─ NO:  → INSERT new row                             │
│  │                                                       │
│  ┌─ Local rows NOT in API response?                      │
│  │  → They were deleted/deactivated on central →          │
│  │   UPDATE active_status=0 locally (soft-delete)        │
│  └─                                                      │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 6: Log Results                                     │
│  Write summary to Windows Event Log:                     │
│    • Timestamp                                           │
│    • API connection status                               │
│    • Holidays fetched (active + inactive count)          │
│    • New inserts, updates, deactivations                 │
│    • Errors (if any)                                     │
└────────────────────────┬─────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  STEP 7: Sleep                                           │
│  Wait until next scheduled sync (next day 01:00 AM).      │
│  Timer interval: configurable, default 24 hours.          │
└──────────────────────────────────────────────────────────┘
```

## Error Handling & Retry

| Scenario | Action |
|----------|--------|
| API unreachable (network error) | Log warning, retry after 15 minutes, max 3 retries |
| Auth failure (401) | Log error, skip sync, notify admin |
| API returns 500 | Log error with response body, retry after 5 minutes, max 2 retries |
| DB connection failure | Log error, abort sync, notify admin |
| Partial sync (some rows fail) | Log per-row errors, continue with remaining rows |

## Local Database Schema

Each on-premise server should have a local copy of the holidays table:

```sql
CREATE TABLE dbo.tb_holiday_server (
    holiday_id    INT PRIMARY KEY,
    holiday_name  NVARCHAR(255) NOT NULL,
    holiday_date  DATE NOT NULL,
    create_by     NVARCHAR(100),
    create_date   DATETIME,
    update_by     NVARCHAR(100),
    update_date   DATETIME,
    active_status INT NOT NULL DEFAULT 1   -- 1=active, 0=inactive
);
```

**Sync strategy:**
- `holiday_id` is the sync key (from central API)
- Never delete rows — use `active_status` flag (soft-delete)
- All fields are overwritten on update except `holiday_id`

## Merge Logic (.NET SQL Example)

```csharp
public void SyncHolidays(SqlConnection conn, List<Holiday> apiHolidays)
{
    // Get existing local IDs
    var localIds = new HashSet<int>();
    using (var cmd = new SqlCommand("SELECT holiday_id FROM dbo.tb_holiday_server", conn))
    using (var reader = cmd.ExecuteReader())
        while (reader.Read())
            localIds.Add(reader.GetInt32(0));

    foreach (var h in apiHolidays)
    {
        if (localIds.Contains(h.holiday_id))
        {
            // UPDATE
            using var cmd = new SqlCommand(@"
                UPDATE dbo.tb_holiday_server SET
                    holiday_name = @name, holiday_date = @date,
                    active_status = @status, update_by = @by,
                    update_date = @updated
                WHERE holiday_id = @id", conn);
            // ... set parameters ...
            cmd.ExecuteNonQuery();
        }
        else
        {
            // INSERT
            using var cmd = new SqlCommand(@"
                INSERT INTO dbo.tb_holiday_server
                (holiday_id, holiday_name, holiday_date, create_by, create_date,
                 update_by, update_date, active_status)
                VALUES (@id, @name, @date, @by, @created, @by, @updated, @status)", conn);
            // ... set parameters ...
            cmd.ExecuteNonQuery();
        }
    }
}
```

## Logging Format

Windows Event Log entries (source: `DLT Holiday Sync`):

```
Level: Information
Summary:
  Synced at: 2026-07-04 01:00:05
  Active holidays: 15
  Inactive holidays: 3
  New inserts: 2
  Updates: 0
  Deactivations: 1
  Duration: 1.2 seconds
  API status: OK
```

```
Level: Warning
Summary:
  API unreachable at: 2026-07-04 01:00:12
  Attempt: 1/3
  Error: System.Net.Http.HttpRequestException: Connection refused
```

```
Level: Error
Summary:
  Sync failed at: 2026-07-04 01:03:00
  Reason: Authentication failed (401)
  Details: Invalid credentials
```

## Performance Considerations

- **Payload size:** ~1–5 KB for typical holiday lists
- **Requests per sync:** 3 (health check + login + fetch)
- **Total bandwidth:** ~15 KB per server per day
- **10 servers:** ~150 KB/day total
- **Execution time:** < 2 seconds per sync
- **Concurrency:** All 10 servers hit at 01:00 AM simultaneously — Vercel handles this easily
