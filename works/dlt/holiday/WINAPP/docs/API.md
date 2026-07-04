# API Reference for WinApp (.NET 4.8)

Base URL: `https://dlt-holiday-admin.vercel.app`

All endpoints require authentication (except `/api/health` and `/api/auth/login`).

---

## Authentication

All API routes (except public ones) require a valid JWT token. After login, the server sets an httpOnly cookie (`dlt_auth_token`). **Include this cookie** in all subsequent requests.

For .NET 4.8, use `HttpClientHandler` with `CookieContainer`:

```csharp
var handler = new HttpClientHandler
{
    UseCookies = true,
    CookieContainer = new CookieContainer()
};
var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
```

---

## Login

```
POST /api/auth/login
```

Authenticate and obtain a JWT session cookie.

**Request:**
```json
{
  "username": "root",
  "password": "Root@@Kai200222",
  "token": "@ETPNqq*9$GD+e9lyqrUa$M4#dG$^wtF"
}
```

**Response (200):**
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Response (401):**
```json
{
  "success": false,
  "error": "Invalid credentials"
}
```

**Notes:**
- The response also sets an httpOnly `dlt_auth_token` cookie via `Set-Cookie` header
- Store the cookie in `CookieContainer` for subsequent requests
- Token expires after 8 hours

---

## Logout

```
POST /api/auth/logout
```

Invalidates the current session cookie.

**Response (200):**
```json
{
  "success": true
}
```

---

## Check Session

```
GET /api/auth/me
```

Returns the current authenticated user, or 401 if expired.

**Response (200):**
```json
{
  "username": "root"
}
```

**Response (401):**
```json
{
  "success": false,
  "error": "Unauthorized"
}
```

---

## Health Check

```
GET /api/health
```

**Public.** No authentication required. Check if the API and database are online.

**Response (200):**
```json
{
  "status": "ok",
  "timestamp": "2026-07-04T01:00:00.000Z",
  "database": {
    "connected": true,
    "server": "146.190.82.168",
    "database": "DLT"
  }
}
```

**Response (500):**
```json
{
  "success": false,
  "error": "..."
}
```

---

## List Holidays

```
GET /api/holidays
```

Returns paginated holidays. **This is the primary endpoint for WinApp sync.**

**Query Parameters:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (set to `10000` for full sync) |
| `search` | string | — | Filter by holiday name (LIKE) |
| `status` | string | — | `1` = active, `0` = inactive |

**Example — fetch all active holidays:**
```
GET /api/holidays?status=1&pageSize=10000
```

**Response (200):**
```json
{
  "success": true,
  "data": [
    {
      "holiday_id": 1,
      "holiday_name": "New Year's Day",
      "holiday_date": "2026-01-01T00:00:00.000Z",
      "create_by": "ADMIN",
      "create_date": "2025-12-01T00:00:00.000Z",
      "update_by": "ADMIN",
      "update_date": "2025-12-15T00:00:00.000Z",
      "active_status": 1
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 10000,
  "totalPages": 1
}
```

**Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `holiday_id` | int | Primary key, auto-increment |
| `holiday_name` | string | Holiday name |
| `holiday_date` | string (ISO 8601) | Holiday date |
| `create_by` | string | Creator username |
| `create_date` | string (ISO 8601) | Creation timestamp |
| `update_by` | string | Last editor username |
| `update_date` | string (ISO 8601) | Last update timestamp |
| `active_status` | int | `1` = active, `0` = inactive (soft-deleted) |

---

## Get Single Holiday

```
GET /api/holidays/{id}
```

**Response (200):**
```json
{
  "success": true,
  "data": {
    "holiday_id": 1,
    "holiday_name": "New Year's Day",
    "holiday_date": "2026-01-01T00:00:00.000Z",
    "active_status": 1,
    ...
  }
}
```

**Response (404):**
```json
{
  "success": false,
  "error": "Holiday not found"
}
```

---

## Export Holidays

```
GET /api/holidays/export?format=csv&search=&status=1
```

Download holidays as CSV or XLSX. Accepts `?format=csv` or `?format=xlsx` plus optional `search` and `status` filters.

**Response:** Binary file download (`Content-Type: text/csv` or `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`)

---

## Error Handling

All errors follow this format:

```json
{
  "success": false,
  "error": "Human-readable error message"
}
```

| HTTP Code | Meaning |
|-----------|---------|
| 200 | Success |
| 400 | Bad request (missing/invalid params) |
| 401 | Unauthorized (login required or expired session) |
| 404 | Not found |
| 500 | Server error |

---

## .NET HttpClient Example

```csharp
using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

public class DltHolidayClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private readonly string _token;

    public DltHolidayClient(string baseUrl, string username, string password, string token)
    {
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        _client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        _baseUrl = baseUrl;
        _username = username;
        _password = password;
        _token = token;
    }

    public async Task<bool> LoginAsync()
    {
        var payload = new
        {
            username = _username,
            password = _password,
            token = _token
        };
        var content = new StringContent(
            JsonConvert.SerializeObject(payload),
            System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", content);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<LoginResponse>(json);
        return result?.Success == true;
    }

    public async Task<List<Holiday>> GetActiveHolidaysAsync()
    {
        var response = await _client.GetAsync("/api/holidays?status=1&pageSize=10000");
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<HolidayListResponse>(json);
        return result?.Data ?? new List<Holiday>();
    }

    public void Dispose() => _client?.Dispose();
}

public class LoginResponse { public bool Success { get; set; } public string Token { get; set; } }
public class HolidayListResponse { public bool Success { get; set; } public List<Holiday> Data { get; set; } }
public class Holiday
{
    public int holiday_id { get; set; }
    public string holiday_name { get; set; }
    public DateTime holiday_date { get; set; }
    public string create_by { get; set; }
    public DateTime create_date { get; set; }
    public string update_by { get; set; }
    public DateTime update_date { get; set; }
    public int active_status { get; set; }
}
```
