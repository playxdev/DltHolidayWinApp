using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Dlt.Holiday.Sync.Helpers
{
    public class CurlResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public bool Ok { get { return ExitCode == 0; } }
    }

    public static class CurlHelper
    {
        private static string _curlPath;

        public static bool Initialize()
        {
            var bundled = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "curl.exe");
            if (File.Exists(bundled))
            {
                _curlPath = bundled;
                TopLineLogger.LogDebug(string.Format("Using bundled curl: {0}", _curlPath));
                return true;
            }

            try
            {
                var psi = new ProcessStartInfo("where", "curl.exe")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                var output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();

                if (p.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    _curlPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    TopLineLogger.LogDebug(string.Format("Found system curl: {0}", _curlPath));
                    return true;
                }
            }
            catch { }

            TopLineLogger.Log(LogLevel.FATAL, "curl.exe not found. Put curl.exe next to the .exe or in PATH.");
            return false;
        }

        public static CurlResult Head(string url, int timeoutSec)
        {
            return Execute(string.Format("-s -I --connect-timeout {0} --max-time {1} \"{2}\"",
                timeoutSec, timeoutSec, url));
        }

        public static CurlResult Get(string url, string cookie, int timeoutSec)
        {
            var cookieFlag = string.IsNullOrEmpty(cookie) ? "" : string.Format("-b \"dlt_auth_token={0}\"", cookie);
            return Execute(string.Format("-s -X GET {0} --connect-timeout {1} --max-time {2} \"{3}\"",
                cookieFlag, timeoutSec, timeoutSec, url));
        }

        public static CurlResult PostJson(string url, string jsonBody, int timeoutSec)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmpFile, jsonBody, Encoding.UTF8);
                return Execute(string.Format(
                    "-s -X POST -H \"Content-Type: application/json\" --data-binary @\"{0}\" --connect-timeout {1} --max-time {1} \"{2}\"",
                    tmpFile, timeoutSec, url));
            }
            finally
            {
                try { File.Delete(tmpFile); } catch { }
            }
        }

        private static CurlResult Execute(string args)
        {
            var result = new CurlResult();

            try
            {
                var psi = new ProcessStartInfo(_curlPath, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var p = Process.Start(psi))
                {
                    result.StdOut = p.StandardOutput.ReadToEnd();
                    result.StdErr = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    result.ExitCode = p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.StdErr = ex.Message;
            }

            return result;
        }
    }
}
