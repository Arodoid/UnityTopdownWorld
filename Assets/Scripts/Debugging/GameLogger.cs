 using UnityEngine;
using System;
using System.Collections.Generic;

namespace VoxelGame.Core.Debugging
{
    public static class GameLogger
    {
        private class LogEntry
        {
            public string Message;
            public LogLevel Level;
            public float LastLogTime;
            public int RepeatCount;
            public float CooldownTime;
        }

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        private static LogLevel currentLevel = LogLevel.Debug;
        private static bool isEnabled = true;
        private static readonly Dictionary<string, LogEntry> recentLogs = new();
        private static readonly float defaultCooldown = 5f; // Seconds between similar logs

        private static void LogWithCooldown(string message, LogLevel level, UnityEngine.Object context = null)
        {
            if (!isEnabled || level < currentLevel) return;

            float currentTime = Time.realtimeSinceStartup;
            string key = $"{level}:{message}";

            if (recentLogs.TryGetValue(key, out LogEntry entry))
            {
                if (currentTime - entry.LastLogTime < entry.CooldownTime)
                {
                    entry.RepeatCount++;
                    return;
                }
                
                // If there were repeated messages, log the count
                if (entry.RepeatCount > 0)
                {
                    string repeatMessage = $"[{DateTime.Now:HH:mm:ss}][{level}] Last message repeated {entry.RepeatCount} times";
                    OutputLog(repeatMessage, level, context);
                }
            }

            // Create or update entry
            recentLogs[key] = new LogEntry
            {
                Message = message,
                Level = level,
                LastLogTime = currentTime,
                RepeatCount = 0,
                CooldownTime = defaultCooldown
            };

            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}][{level}] {message}";
            OutputLog(formattedMessage, level, context);
        }

        private static void OutputLog(string message, LogLevel level, UnityEngine.Object context)
        {
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(message, context);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(message, context);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message, context);
                    break;
            }
        }

        // Public logging methods
        public static void LogDebug(string message, UnityEngine.Object context = null) 
            => LogWithCooldown(message, LogLevel.Debug, context);
        
        public static void LogInfo(string message, UnityEngine.Object context = null) 
            => LogWithCooldown(message, LogLevel.Info, context);
        
        public static void LogWarning(string message, UnityEngine.Object context = null) 
            => LogWithCooldown(message, LogLevel.Warning, context);
        
        public static void LogError(string message, UnityEngine.Object context = null) 
            => LogWithCooldown(message, LogLevel.Error, context);

        // Control methods
        public static void SetLogLevel(LogLevel level) => currentLevel = level;
        public static void Enable() => isEnabled = true;
        public static void Disable() => isEnabled = false;
    }
}