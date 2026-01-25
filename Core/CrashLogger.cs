using System;
using System.IO;
using System.Text;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Centralized crash logging service
/// </summary>
public static class CrashLogger
{
    private const string CrashLogFile = "crash.log";

    public static void Log(Exception ex)
    {
        var report = new StringBuilder();
        report.AppendLine("=== DepthClockWallpaper Crash Report ===");
        report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Exception Type: {ex.GetType().FullName}");
        report.AppendLine($"Message: {ex.Message}");
        report.AppendLine();
        report.AppendLine("Stack Trace:");
        report.AppendLine(ex.StackTrace);
        report.AppendLine("=== End of Crash Report ===");
        report.AppendLine();

        var logPath = Path.Combine(AppContext.BaseDirectory, CrashLogFile);
        File.AppendAllText(logPath, report.ToString());

        Console.WriteLine($"Crash logged to: {logPath}");
    }

    public static string GetCrashLogPath()
    {
        return Path.Combine(AppContext.BaseDirectory, CrashLogFile);
    }

    public static bool CrashLogExists()
    {
        return File.Exists(GetCrashLogPath());
    }
}
