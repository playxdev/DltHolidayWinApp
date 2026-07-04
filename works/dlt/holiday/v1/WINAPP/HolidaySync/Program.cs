using System;
using System.IO;
using System.Net;
using System.Threading;
using Dlt.Holiday.Sync.Helpers;

namespace Dlt.Holiday.Sync
{
    internal class Program
    {
        static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.Net.DontEnableSchUseStrongCrypto", false);
            AppContext.SetSwitch("Switch.System.Net.DontEnableSystemDefaultTlsVersions", false);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var iniFilePath = ResolveIniFilePath(args);

            try
            {
                TopLineLogger.Initialize(AppDomain.CurrentDomain.BaseDirectory);

                var engine = new SyncEngine(iniFilePath);
                engine.Run();

                Thread.Sleep(5000);
                return 0;
            }
            catch (Exception ex)
            {
                TopLineLogger.LogError("Unhandled error", ex);
                Thread.Sleep(5000);
                Environment.Exit(1);
                return 1;
            }
        }

        private static string ResolveIniFilePath(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                var argPath = args[0].Trim();
                if (File.Exists(argPath))
                    return argPath;
            }

            var defaultPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "edriving.ini");

            if (File.Exists(defaultPath))
                return defaultPath;

            var parentPath = Path.Combine(
                Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName,
                "edriving.ini");

            if (File.Exists(parentPath))
                return parentPath;

            throw new FileNotFoundException(
                string.Format("edriving.ini not found at {0} or {1}", defaultPath, parentPath));
        }
    }
}
