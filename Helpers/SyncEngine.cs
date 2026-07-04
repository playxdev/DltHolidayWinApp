using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Dlt.Holiday.Sync.Models;
using Newtonsoft.Json;

namespace Dlt.Holiday.Sync.Helpers
{
    public class SyncEngine
    {
        private readonly string _serverName;
        private readonly string _connectionString;
        private readonly string _apiBaseUrl;
        private readonly string _apiHolidaysEndpoint;
        private readonly string _apiLoginEndpoint;
        private readonly string _authUsername;
        private readonly string _authPassword;
        private readonly string _authToken;
        private readonly string _iniFilePath;
        private readonly int _httpTimeoutSeconds;
        private readonly int _dbConnectTimeoutSeconds;

        private string _jwtToken;
        private SqlConnection _dbConnection;
        private SqlTransaction _activeTransaction;
        private bool _transactionOpen;

        public SyncEngine(string iniFilePath)
        {
            _iniFilePath = iniFilePath;
            _httpTimeoutSeconds = 15;
            _dbConnectTimeoutSeconds = 10;
            _apiBaseUrl = "https://dlt-holiday-admin.vercel.app";
            _apiHolidaysEndpoint = _apiBaseUrl + "/api/holidays";
            _apiLoginEndpoint = _apiBaseUrl + "/api/auth/login";

            var ini = new IniParser(iniFilePath);

            _serverName = ini.GetValue("SERVER_INFO", "SERVER_NAME", Environment.MachineName);
            _connectionString = SanitizeConnectionString(
                ini.GetValue("DATABASE", "CONNECTIONSTRING", string.Empty));

            _authUsername = ini.GetValue("API_AUTH", "USERNAME", string.Empty);
            _authPassword = ini.GetValue("API_AUTH", "PASSWORD", string.Empty);
            _authToken = ini.GetValue("API_AUTH", "TOKEN", string.Empty);

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                var odbcType = ini.GetValue("ODBC_INFO", "ODBC_TYPE", string.Empty);
                var odbcUser = ini.GetValue("ODBC_INFO", "ODBC_USER", string.Empty);
                var odbcPassword = ini.GetValue("ODBC_INFO", "ODBC_PASSWORD", string.Empty);
                var odbcName = ini.GetValue("ODBC_INFO", "ODBC_NAME", string.Empty);
                var odbcDatabase = ini.GetValue("ODBC_INFO", "ODBC_DATABASE_NAME", string.Empty);

                if (!string.IsNullOrWhiteSpace(odbcType))
                {
                    _connectionString = string.Format(
                        "DSN={0};UID={1};PWD={2};DATABASE={3};",
                        odbcName, odbcUser, odbcPassword, odbcDatabase);
                }
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException(
                    "No valid connection string found. Check [DATABASE] CONNECTIONSTRING or [ODBC_INFO] section.");
            }

            TopLineLogger.Log(LogLevel.SUCCESS, string.Format("Loaded INI ({0})", _serverName));
            TopLineLogger.LogDebug(string.Format("Connection: {0}", MaskConnectionString(_connectionString)));

            if (!CurlHelper.Initialize())
                throw new Exception("curl.exe not available");
        }

        public void Run()
        {
            List<HolidayDto> holidays = null;

            try
            {
                TopLineLogger.Log(LogLevel.INFO, string.Format("Sync started for '{0}'", _serverName));

                if (!PreFlightHttpCheck())
                {
                    TopLineLogger.Log(LogLevel.FATAL, "Can't connect Vercel");
                    Thread.Sleep(5000);
                    Environment.Exit(1);
                }

                if (!PreFlightDbCheck())
                {
                    TopLineLogger.Log(LogLevel.FATAL, "Can't connect Local Database");
                    Thread.Sleep(5000);
                    Environment.Exit(1);
                }

                if (!Login())
                {
                    TopLineLogger.Log(LogLevel.FATAL, "Can't login to API");
                    Thread.Sleep(5000);
                    Environment.Exit(1);
                }

                holidays = DownloadHolidays();

                if (holidays == null || holidays.Count == 0)
                {
                    TopLineLogger.Log(LogLevel.FATAL, "Downloaded 0 records from API");
                    Thread.Sleep(5000);
                    Environment.Exit(1);
                }

                TopLineLogger.Log(LogLevel.INFO, string.Format("Downloaded {0} records from API", holidays.Count));

                SyncToDatabase(holidays);

                TopLineLogger.Log(LogLevel.DONE, string.Format("UPDATE table on '{0}' ({1} records)", _serverName, holidays.Count));
            }
            catch (Exception ex)
            {
                RollbackTransaction();
                TopLineLogger.LogError(string.Format("Sync failed on '{0}'", _serverName), ex);
                Thread.Sleep(5000);
                Environment.Exit(1);
            }
            finally
            {
                DisposeDbConnection();

                /*
                 * TODO (V2): Telegram Bot Notification
                 *
                 * Send a Telegram Bot notification with SERVER_NAME and sync status.
                 * string botToken = read from INI [TELEGRAM] BOT_TOKEN
                 * string chatId   = read from INI [TELEGRAM] CHAT_ID
                 * string status   = success ? "Success" : "Failed";
                 * string message  = $"{_serverName} Sync {status}";
                 * SendTelegramNotification(botToken, chatId, message);
                 */

                TopLineLogger.Log(LogLevel.INFO, "Sync engine finished");
            }
        }

        private bool PreFlightHttpCheck()
        {
            try
            {
                var result = CurlHelper.Head(_apiBaseUrl, _httpTimeoutSeconds);

                if (result.Ok)
                {
                    TopLineLogger.Log(LogLevel.SUCCESS, "Connected Vercel");
                    return true;
                }

                TopLineLogger.Log(LogLevel.FATAL, string.Format("Vercel unreachable: {0}", result.StdErr.Trim()));
                return false;
            }
            catch (Exception ex)
            {
                TopLineLogger.LogError("Can't connect Vercel", ex);
                return false;
            }
        }

        private bool PreFlightDbCheck()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    using (var cmd = new SqlCommand("SELECT 1;", conn))
                    {
                        cmd.CommandTimeout = _dbConnectTimeoutSeconds;
                        cmd.ExecuteScalar();
                    }
                }

                TopLineLogger.Log(LogLevel.SUCCESS, "Connected Local Database");
                return true;
            }
            catch (Exception ex)
            {
                TopLineLogger.LogError("Can't connect Local Database", ex);
                return false;
            }
        }

        private bool Login()
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new
                {
                    username = _authUsername,
                    password = _authPassword,
                    token = _authToken
                });

                var result = CurlHelper.PostJson(_apiLoginEndpoint, payload, _httpTimeoutSeconds);

                if (!result.Ok)
                {
                    TopLineLogger.Log(LogLevel.FATAL, string.Format("Login failed: {0}", result.StdErr.Trim()));
                    return false;
                }

                var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(result.StdOut);

                if (loginResponse == null || !loginResponse.Success || string.IsNullOrEmpty(loginResponse.Token))
                {
                    TopLineLogger.Log(LogLevel.FATAL, string.Format("Login failed: {0}", result.StdOut.Trim()));
                    return false;
                }

                _jwtToken = loginResponse.Token;
                TopLineLogger.Log(LogLevel.SUCCESS, "Logged in to API");
                return true;
            }
            catch (Exception ex)
            {
                TopLineLogger.LogError("Can't login to API", ex);
                return false;
            }
        }

        private List<HolidayDto> DownloadHolidays()
        {
            var allHolidays = new List<HolidayDto>();
            var currentPage = 1;
            var totalPages = 1;
            var pageSize = 250;

            do
            {
                var url = string.Format("{0}?page={1}&pageSize={2}",
                    _apiHolidaysEndpoint, currentPage, pageSize);

                var result = CurlHelper.Get(url, _jwtToken, 60);

                if (!result.Ok)
                {
                    throw new Exception(string.Format(
                        "API HTTP error: {0}", result.StdErr.Trim()));
                }

                if (string.IsNullOrWhiteSpace(result.StdOut))
                    throw new Exception("API returned empty body");

                var apiResponse = JsonConvert.DeserializeObject<PaginatedApiResponse<HolidayDto>>(result.StdOut);

                if (apiResponse == null)
                    throw new Exception("Failed to deserialize API response");

                if (!apiResponse.Success)
                    throw new Exception(string.Format(
                        "API error: {0}", apiResponse.Error ?? "N/A"));

                totalPages = apiResponse.TotalPages;

                if (apiResponse.Data != null && apiResponse.Data.Count > 0)
                    allHolidays.AddRange(apiResponse.Data);

                TopLineLogger.Log(LogLevel.INFO, string.Format(
                    "Fetched page {0}/{1} ({2} records)",
                    currentPage, totalPages, apiResponse.Data?.Count ?? 0));

                currentPage++;
            }
            while (currentPage <= totalPages);

            return allHolidays;
        }

        private void SyncToDatabase(List<HolidayDto> holidays)
        {
            _dbConnection = new SqlConnection(_connectionString);
            _dbConnection.Open();

            using (_activeTransaction = _dbConnection.BeginTransaction())
            {
                _transactionOpen = true;

                try
                {
                    using (var deleteCmd = new SqlCommand(
                        "DELETE FROM tb_holiday;", _dbConnection, _activeTransaction))
                    {
                        deleteCmd.CommandTimeout = 30;
                        var deletedCount = deleteCmd.ExecuteNonQuery();
                        TopLineLogger.Log(LogLevel.INFO, string.Format("Deleted {0} rows from tb_holiday", deletedCount));
                    }

                    var dataTable = CreateHolidayDataTable(holidays);

                    using (var bulkCopy = new SqlBulkCopy(
                        _dbConnection, SqlBulkCopyOptions.KeepIdentity, _activeTransaction))
                    {
                        bulkCopy.DestinationTableName = "tb_holiday";
                        bulkCopy.BatchSize = 500;
                        bulkCopy.BulkCopyTimeout = 120;

                        bulkCopy.ColumnMappings.Add("holiday_id", "holiday_id");
                        bulkCopy.ColumnMappings.Add("holiday_name", "holiday_name");
                        bulkCopy.ColumnMappings.Add("holiday_date", "holiday_date");
                        bulkCopy.ColumnMappings.Add("create_by", "create_by");
                        bulkCopy.ColumnMappings.Add("create_date", "create_date");
                        bulkCopy.ColumnMappings.Add("update_by", "update_by");
                        bulkCopy.ColumnMappings.Add("update_date", "update_date");
                        bulkCopy.ColumnMappings.Add("active_status", "active_status");

                        bulkCopy.WriteToServer(dataTable);
                    }

                    ReseedIdentity(_dbConnection, _activeTransaction);

                    _activeTransaction.Commit();
                    _transactionOpen = false;
                }
                catch
                {
                    RollbackTransaction();
                    throw;
                }
            }
        }

        private DataTable CreateHolidayDataTable(List<HolidayDto> holidays)
        {
            var table = new DataTable("tb_holiday");

            table.Columns.Add("holiday_id", typeof(long));
            table.Columns.Add("holiday_name", typeof(string));
            table.Columns.Add("holiday_date", typeof(DateTime));
            table.Columns.Add("create_by", typeof(string));
            table.Columns.Add("create_date", typeof(DateTime));
            table.Columns.Add("update_by", typeof(string));
            table.Columns.Add("update_date", typeof(DateTime));
            table.Columns.Add("active_status", typeof(int));

            foreach (var h in holidays)
            {
                var row = table.NewRow();

                row["holiday_id"] = h.HolidayId;
                row["holiday_name"] = (object)h.HolidayName ?? DBNull.Value;

                if (DateTime.TryParse(h.HolidayDate, out var holidayDate))
                    row["holiday_date"] = holidayDate.Date;
                else
                    row["holiday_date"] = DBNull.Value;

                row["create_by"] = (object)h.CreateBy ?? DBNull.Value;

                if (DateTime.TryParse(h.CreateDate, out var createDate))
                    row["create_date"] = createDate;
                else
                    row["create_date"] = DBNull.Value;

                row["update_by"] = (object)h.UpdateBy ?? DBNull.Value;

                if (DateTime.TryParse(h.UpdateDate, out var updateDate))
                    row["update_date"] = updateDate;
                else
                    row["update_date"] = DBNull.Value;

                row["active_status"] = h.ActiveStatus;

                table.Rows.Add(row);
            }

            return table;
        }

        private static void ReseedIdentity(SqlConnection conn, SqlTransaction tran)
        {
            using (var cmd = new SqlCommand(
                "DECLARE @maxId BIGINT = (SELECT ISNULL(MAX(holiday_id), 0) FROM tb_holiday); DBCC CHECKIDENT ('tb_holiday', RESEED, @maxId);",
                conn, tran))
            {
                cmd.CommandTimeout = 10;
                cmd.ExecuteNonQuery();
            }
        }

        private void RollbackTransaction()
        {
            if (_activeTransaction != null && _transactionOpen)
            {
                try
                {
                    _activeTransaction.Rollback();
                    _transactionOpen = false;
                    TopLineLogger.Log(LogLevel.INFO, "Transaction rolled back");
                }
                catch (Exception rollbackEx)
                {
                    TopLineLogger.LogError("Failed to rollback transaction", rollbackEx);
                }
            }
        }

        private void DisposeDbConnection()
        {
            if (_activeTransaction != null)
            {
                try { _activeTransaction.Dispose(); } catch { }
                _activeTransaction = null;
            }

            if (_dbConnection != null)
            {
                try
                {
                    if (_dbConnection.State != ConnectionState.Closed)
                        _dbConnection.Close();
                    _dbConnection.Dispose();
                }
                catch { }

                _dbConnection = null;
            }
        }

        private static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return "(empty)";

            var masked = connectionString;

            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, @"Password=([^;]+)", "Password=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, @"Pwd=([^;]+)", "Pwd=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, @"User ID=([^;]+)", "User ID=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, @"Uid=([^;]+)", "Uid=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return masked;
        }

        private static string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            var result = System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"(Application Name|Command Timeout|Persist Security Info|Pooling|MultipleActiveResultSets)\s*=\s*(""[^""]*""|[^;]*);?",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            result = System.Text.RegularExpressions.Regex.Replace(
                result, @";{2,}", ";");

            return result.TrimEnd(';');
        }
    }
}
