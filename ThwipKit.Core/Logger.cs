using System;
using System.IO;
using System.Text;

namespace ThwipKit.Core
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static string _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThwipKit", "logs");

        private static string _logFilePath = "";
        private static LogLevel _minimumLevel = LogLevel.Info;
        private static bool _initialized;

        public static string LogDirectory
        {
            get => _logDirectory;
            set
            {
                _logDirectory = value;
                _initialized = false;
            }
        }

        public static LogLevel MinimumLevel
        {
            get => _minimumLevel;
            set => _minimumLevel = value;
        }

        public static string LogFilePath => _logFilePath;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                _logFilePath = Path.Combine(_logDirectory, $"smt_{DateTime.Now:yyyyMMdd}.log");
                _initialized = true;

                Log(LogLevel.Info, "=== Logger initialized ===");
            }
            catch
            {
                _initialized = false;
            }
        }

        public static void Log(LogLevel level, string message)
        {
            if (level < _minimumLevel) return;

            Initialize();
            if (!_initialized) return;

            try
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently ignore logging errors
            }
        }

        public static void LogDebug(string message) => Log(LogLevel.Debug, message);
        public static void LogInfo(string message) => Log(LogLevel.Info, message);
        public static void LogWarning(string message) => Log(LogLevel.Warning, message);
        public static void LogError(string message) => Log(LogLevel.Error, message);

        public static void LogException(Exception ex, string context = "")
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(context))
            {
                sb.AppendLine($"Context: {context}");
            }
            sb.AppendLine($"Exception: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"StackTrace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"InnerException: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"InnerMessage: {ex.InnerException.Message}");
            }
            Log(LogLevel.Error, sb.ToString());
        }

        public static void CleanOldLogs(int maxDaysToKeep = 30)
        {
            try
            {
                if (!Directory.Exists(_logDirectory)) return;

                DateTime cutoff = DateTime.Now.AddDays(-maxDaysToKeep);
                foreach (string file in Directory.GetFiles(_logDirectory, "smt_*.log"))
                {
                    if (File.GetCreationTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // Silently ignore cleanup errors
            }
        }
    }
}