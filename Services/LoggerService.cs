using System;
using System.IO;

namespace MovieManagerDesktop.Services
{
    public static class LoggerService
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static LoggerService()
        {
            try
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                
                var logDir = Path.Combine(appData, "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                
                LogFilePath = Path.Combine(logDir, "app.log");
            }
            catch
            {
                // Fallback to app directory if appdata fails
                LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
            }
        }

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Warning(string message)
        {
            WriteLog("WARN", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex == null ? message : $"{message}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace:\n{ex.StackTrace}";
            WriteLog("ERROR", fullMessage);
        }

        private static void WriteLog(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logEntry);
                }
            }
            catch
            {
                // Ignore logging errors to prevent recursive crashes
            }
        }
    }
}
