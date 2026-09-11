using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MovieManagerDesktop.Services
{
    /// <summary>
    /// Lightweight, ultra-fast asynchronous logging service inspired by modern standards (Movie Collection / Serilog).
    /// Uses a non-blocking background queue so disk I/O never slows down scanning or UI operations.
    /// Includes automatic log rotation with a 5 MB file size limit to protect disk space.
    /// Automatically sanitizes and redacts any sensitive credentials, API keys, tokens, or license keys.
    /// </summary>
    public static class LoggerService
    {
        public static string LogDirectory { get; }
        public static string LogFilePath { get; }
        private static readonly string OldLogFilePath;

        private const long MaxLogFileSizeBytes = 5 * 1024 * 1024; // 5 MB max per log file
        private static readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true
        });

        public static event EventHandler<string>? LogAdded;

        static LoggerService()
        {
            try
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManager");
                LogDirectory = Path.Combine(appData, "Logs");

                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                LogFilePath = Path.Combine(LogDirectory, "app.log");
                OldLogFilePath = Path.Combine(LogDirectory, "app.old.log");

                // Legacy migration
                var legacyLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieManagerDesktop", "Logs", "app.log");
                if (!File.Exists(LogFilePath) && File.Exists(legacyLog))
                {
                    try { File.Copy(legacyLog, LogFilePath, true); } catch { }
                }

                // Initial size check: if existing log is > 5 MB, rotate immediately to restore top speed
                RotateIfNecessary();
            }
            catch
            {
                LogDirectory = AppDomain.CurrentDomain.BaseDirectory;
                LogFilePath = Path.Combine(LogDirectory, "app.log");
                OldLogFilePath = Path.Combine(LogDirectory, "app.old.log");
            }

            // Start background consumer thread that flushes queue to disk asynchronously
            Task.Run(ProcessLogQueueAsync);
        }

        public static void Info(string message)
        {
            Enqueue("INFO", message);
        }

        public static void Warning(string message)
        {
            Enqueue("WARN", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex == null 
                ? message 
                : $"{message}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace:\n{ex.StackTrace}";
            Enqueue("ERROR", fullMessage);
        }

        private static void Enqueue(string level, string message)
        {
            try
            {
                // Redact all keys, secrets, tokens, passwords and license keys
                string safeMessage = SanitizeSensitiveData(message);

                string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logEntry = $"{timeStr} | {level,-5} | {safeMessage}";

                if (level == "ERROR")
                {
                    logEntry += Environment.NewLine + "--------------------------------------------------";
                }

                // Fire event for any listeners
                try { LogAdded?.Invoke(null, logEntry); } catch { }

                // Non-blocking write to channel (0ms latency for caller)
                _logChannel.Writer.TryWrite(logEntry);
            }
            catch
            {
                // Never crash the caller thread on logging failure
            }
        }

        /// <summary>
        /// Automatically masks sensitive information like API keys, license codes, tokens, and passwords.
        /// </summary>
        private static string SanitizeSensitiveData(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            try
            {
                // 1. Mask query parameter keys: api_key=..., apikey=..., token=..., secret=..., etc.
                text = Regex.Replace(text, @"(api_key|apikey|token|secret|access_token|key|api-key)=([a-zA-Z0-9_\-\.]{5,})", "$1=***", RegexOptions.IgnoreCase);

                // 2. Mask Authorization Bearer tokens
                text = Regex.Replace(text, @"Bearer\s+[a-zA-Z0-9_\-\.]{10,}", "Bearer ***", RegexOptions.IgnoreCase);

                // 3. Mask MovieManager license keys: MM-XXXX-XXXX-XXXX-XXXX
                text = Regex.Replace(text, @"MM-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}-[A-Za-z0-9]{4}", "MM-****-****-****-****", RegexOptions.IgnoreCase);

                // 4. Mask 32-character hexadecimal hashes/keys (like TMDB/Subdl md5 keys)
                text = Regex.Replace(text, @"\b[a-fA-F0-9]{32}\b", "***");

                // 5. Mask passwords in strings like password=... or pwd=...
                text = Regex.Replace(text, @"(password|passwd|pwd)=([^\s&]+)", "$1=***", RegexOptions.IgnoreCase);
            }
            catch
            {
                // In case of any regex failure, return original text safely
            }

            return text;
        }

        private static async Task ProcessLogQueueAsync()
        {
            var reader = _logChannel.Reader;

            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                try
                {
                    RotateIfNecessary();

                    using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
                    using var writer = new StreamWriter(stream, Encoding.UTF8);

                    while (reader.TryRead(out var logEntry))
                    {
                        await writer.WriteLineAsync(logEntry).ConfigureAwait(false);
                    }

                    await writer.FlushAsync().ConfigureAwait(false);
                }
                catch
                {
                    // If disk write fails (e.g. disk full), wait briefly before retrying
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        private static void RotateIfNecessary()
        {
            try
            {
                if (File.Exists(LogFilePath))
                {
                    var fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Length >= MaxLogFileSizeBytes)
                    {
                        if (File.Exists(OldLogFilePath))
                        {
                            File.Delete(OldLogFilePath);
                        }
                        File.Move(LogFilePath, OldLogFilePath);
                    }
                }
            }
            catch
            {
                // Ignore rotation errors
            }
        }
    }
}
