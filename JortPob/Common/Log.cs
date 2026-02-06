using HKX2;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

#nullable enable

namespace JortPob.Common
{
    public class Lort
    {
        public static ConcurrentBag<string> mainOutput { get; } = new();
        public static ConcurrentBag<string> debugOutput { get; } = new();
        public static string progressOutput { get; private set; } = string.Empty;
        public static int total { get; private set; }
        public static int current { get; private set; }
        public static bool update { get; set; }
        public static string logFilePath { get; private set; } = string.Empty;

        public static void Initialize()
        {
            total = 0;
            current = 0;
            update = false;

            Directory.CreateDirectory(Path.Combine(Const.OUTPUT_PATH, "logs"));

            logFilePath = Path.Combine(Const.OUTPUT_PATH, @$"logs\jortpob-log-{DateTime.UtcNow.ToLongTimeString().Replace(":", "").Replace(" PM", "")}.txt");
            File.WriteAllText(logFilePath, "");
        }

        public enum Type
        {
            Main,
            Debug
        }

        public static void Log(string message, Type type)
        {
            switch (type)
            {
                case Type.Main:
                    mainOutput.Add(message); break;
                case Type.Debug:
                    debugOutput.Add(message); break;
            }
            update = true;
            AppendTextToLog(message, type);
        }

        public static void LogDebug(string message)
        {
            debugOutput.Add(message);
            update = true;
            AppendTextToLog(message, Type.Debug);
        }

        public static void LogMain(string message)
        {
            mainOutput.Add(message);
            update = true;
            AppendTextToLog(message, Type.Main);
        }

        public static void NewTask(string task, int max)
        {
            progressOutput = $"{task}";
            current = 0;
            total = max;
            update = true;
        }

        public static void TaskIterate()
        {
            current = Math.Min(current+1, total);
            update = true;
        }

        private static void AppendTextToLog(string message, Type type)
        {
            if (string.IsNullOrEmpty(logFilePath))
                return;

            switch (type)
            {
                case Type.Main:
                    Task.Run(async () => await File.AppendAllTextAsync(logFilePath, $"[MAIN] {message}\n"));
                    break;
                case Type.Debug:
                    Task.Run(async () => await File.AppendAllTextAsync(logFilePath, $"[DEBUG] {message}\n"));
                    break;
            }
        }
    }
}
