using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Ловит managed-ошибки и исключения на билде и пишет читаемый лог сессии
/// (плюс системную информацию) в persistentDataPath/BugReports/.
/// Инициализируется автоматически до загрузки сцены — не требует размещения в сцене.
///
/// Где искать отчёты на Windows-билде:
///   %USERPROFILE%\AppData\LocalLow\havocstudioteam\Endless Tower Survivors\BugReports\
/// Нативные краши Unity дополнительно пишет в:
///   %USERPROFILE%\AppData\Local\Temp\havocstudioteam\Endless Tower Survivors\Crashes\
/// </summary>
public static class CrashReporter
{
    private const int MaxSessionsToKeep = 20;

    private static string logFilePath;
    private static readonly object gate = new object();
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (initialized) return;
        initialized = true;

        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "BugReports");
            Directory.CreateDirectory(dir);
            PruneOldSessions(dir);

            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(dir, "session_" + stamp + ".log");

            WriteHeader();

            Application.logMessageReceivedThreaded += OnLog;
            Application.quitting += OnQuit;
        }
        catch
        {
            // Логгер никогда не должен ронять игру.
        }
    }

    private static void WriteHeader()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Endless Tower Survivors — Session Log ===");
        sb.AppendLine("Time:         " + DateTime.Now);
        sb.AppendLine("App version:  " + Application.version);
        sb.AppendLine("Unity:        " + Application.unityVersion);
        sb.AppendLine("Platform:     " + Application.platform);
        sb.AppendLine("OS:           " + SystemInfo.operatingSystem);
        sb.AppendLine("CPU:          " + SystemInfo.processorType + " x" + SystemInfo.processorCount);
        sb.AppendLine("RAM:          " + SystemInfo.systemMemorySize + " MB");
        sb.AppendLine("GPU:          " + SystemInfo.graphicsDeviceName +
                      " (" + SystemInfo.graphicsDeviceType + ", " + SystemInfo.graphicsMemorySize + " MB)");
        sb.AppendLine("Graphics API: " + SystemInfo.graphicsDeviceVersion);
        sb.AppendLine("Screen:       " + Screen.width + "x" + Screen.height);
        sb.AppendLine("Log path:     " + logFilePath);
        sb.AppendLine("=============================================");
        File.AppendAllText(logFilePath, sb.ToString());
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        try
        {
            lock (gate)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + type + ": " + condition);
                if (!string.IsNullOrEmpty(stackTrace))
                    sb.AppendLine(stackTrace.TrimEnd());
                File.AppendAllText(logFilePath, sb.ToString());
            }
        }
        catch
        {
            // Не мешаем игре из-за проблем записи в лог.
        }
    }

    private static void OnQuit()
    {
        try
        {
            File.AppendAllText(logFilePath,
                "\n[Сессия завершена штатно в " + DateTime.Now.ToString("HH:mm:ss") + "]\n");
        }
        catch { }
    }

    /// <summary>Оставляем только последние MaxSessionsToKeep логов, чтобы папка не разрасталась.</summary>
    private static void PruneOldSessions(string dir)
    {
        try
        {
            string[] files = Directory.GetFiles(dir, "session_*.log");
            if (files.Length <= MaxSessionsToKeep) return;
            Array.Sort(files, StringComparer.Ordinal);
            int toDelete = files.Length - MaxSessionsToKeep;
            for (int i = 0; i < toDelete; i++)
                File.Delete(files[i]);
        }
        catch { }
    }
}
