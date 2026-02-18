using System;
using System.IO;

namespace MultiPlatformXamarinExample
{
    public static class Logger
    {
        private static string _logFilePath;

        static Logger()
        {
            // Get app's document directory
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _logFilePath = Path.Combine(documentsPath, "braincloud_log.txt");

            // Create file if it doesn't exist
            if (!File.Exists(_logFilePath))
            {
                File.WriteAllText(_logFilePath, $"=== BrainCloud Log Started {DateTime.Now} ===\n\n");
            }
        }

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";

                // Write to file
                File.AppendAllText(_logFilePath, logEntry);

                // Also write to console for debugging
                Console.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger Error: {ex.Message}");
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            string errorMsg = ex != null
                ? $"ERROR: {message} - {ex.Message}\n{ex.StackTrace}"
                : $"ERROR: {message}";
            Log(errorMsg);
        }

        public static string GetLogFilePath()
        {
            return _logFilePath;
        }

        public static string ReadLog()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    return File.ReadAllText(_logFilePath);
                }
                return "Log file not found.";
            }
            catch (Exception ex)
            {
                return $"Error reading log: {ex.Message}";
            }
        }

        public static void ClearLog()
        {
            try
            {
                File.WriteAllText(_logFilePath, $"=== Log Cleared {DateTime.Now} ===\n\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing log: {ex.Message}");
            }
        }
    }
}

