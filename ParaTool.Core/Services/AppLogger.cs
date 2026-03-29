using System.Collections.Concurrent;

namespace ParaTool.Core.Services;

/// <summary>
/// Simple file logger. Writes to %LocalAppData%/ParaTool/logs/paratool_YYYY-MM-DD.log
/// Thread-safe, auto-rotates daily, cleans up logs older than 7 days on startup.
/// </summary>
public static class AppLogger
{
    private static readonly string LogDir = Path.Combine(ProfileService.GetStorageDir(), "logs");
    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly object _flushLock = new();
    private static string? _currentDate;
    private static StreamWriter? _writer;
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            Directory.CreateDirectory(LogDir);
            CleanOldLogs();
        }
        catch { }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");
    public static void Debug(string message) => Write("DEBUG", message);

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        _queue.Enqueue(line);
        Flush();
    }

    private static void Flush()
    {
        if (!_initialized) return;
        lock (_flushLock)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (_currentDate != today)
                {
                    _writer?.Dispose();
                    _writer = null;
                    _currentDate = today;
                }

                _writer ??= new StreamWriter(
                    Path.Combine(LogDir, $"paratool_{_currentDate}.log"), append: true)
                { AutoFlush = true };

                while (_queue.TryDequeue(out var line))
                    _writer.WriteLine(line);
            }
            catch { }
        }
    }

    private static void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.GetFiles(LogDir, "paratool_*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { }
    }

    /// <summary>Returns path to current log file (for user to share).</summary>
    public static string? GetCurrentLogPath()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var path = Path.Combine(LogDir, $"paratool_{today}.log");
        return File.Exists(path) ? path : null;
    }

    /// <summary>Returns path to logs directory.</summary>
    public static string GetLogDir() => LogDir;
}
