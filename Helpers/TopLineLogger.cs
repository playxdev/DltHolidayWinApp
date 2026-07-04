using System;
using System.IO;

namespace Dlt.Holiday.Sync.Helpers
{
    public enum LogLevel { INFO, SUCCESS, FATAL, WARNING, DONE, DEBUG }

    public static class TopLineLogger
    {
        private static readonly object LockObject = new object();
        private static string _logFilePath;
        private static bool _debugMode = true;

        public static void Initialize(string baseDirectory)
        {
            _logFilePath = Path.Combine(baseDirectory, "sync_event.log");
        }

        public static void Log(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var entry = string.Format("[{0}]{1} {2} > {3}",
                timestamp, GetEmoji(level), level, message);

            lock (LockObject)
            {
                try
                {
                    EnsureInitialized();

                    string existingContent = string.Empty;
                    if (File.Exists(_logFilePath))
                    {
                        existingContent = File.ReadAllText(_logFilePath);
                    }

                    File.WriteAllText(_logFilePath, entry + Environment.NewLine + existingContent);
                }
                catch
                {
                }
            }

            if (level != LogLevel.DEBUG)
                WriteConsole(level, entry);
        }

        public static void LogError(string message, Exception ex)
        {
            if (ex == null)
            {
                Log(LogLevel.FATAL, message);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("({0})", ex.Message ?? "N/A");

            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendFormat(" -> {0}", inner.Message ?? "N/A");
                inner = inner.InnerException;
            }

            Log(LogLevel.FATAL, string.Format("{0} [{1}] {2}", message, ex.GetType().Name, sb));

            if (_debugMode)
                Log(LogLevel.DEBUG, string.Format("Stack: {0}", ex.StackTrace ?? "N/A"));
        }

        public static void LogDebug(string message)
        {
            Log(LogLevel.DEBUG, message);
        }

        private static void WriteConsole(LogLevel level, string entry)
        {
            try
            {
                var color = Console.ForegroundColor;

                switch (level)
                {
                    case LogLevel.FATAL:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.WARNING:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.SUCCESS:
                    case LogLevel.DONE:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                }

                Console.WriteLine(entry);
                Console.ForegroundColor = color;
            }
            catch
            {
            }
        }

        private static string GetEmoji(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.SUCCESS: return "\U0001F7E2";
                case LogLevel.FATAL:   return "\U0001F534";
                case LogLevel.WARNING: return "\U0001F7E1";
                case LogLevel.DONE:    return "\u2705";
                case LogLevel.DEBUG:   return "\U0001F41B";
                default:               return "\U0001F535";
            }
        }

        private static void EnsureInitialized()
        {
            if (string.IsNullOrEmpty(_logFilePath))
            {
                _logFilePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "sync_event.log");
            }
        }
    }
}
