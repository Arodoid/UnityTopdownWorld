using UnityEngine;
using System;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LogProcessor : MonoBehaviour
{
    [SerializeField] private string logFilePath = "Assets/Extra/log.txt";
    [SerializeField] private string outputFilePath = "Assets/Extra/logfiltered.txt";

    [ContextMenu("Process Logs")]
    public void ProcessLogs()
    {
        try
        {
            // Read all lines from the log file
            var logLines = File.ReadAllLines(logFilePath);

            // Process logs to extract meaningful messages
            var filteredLines = logLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith("UnityEngine.") && 
                              !line.Contains(":ExecuteTasks") && 
                              !line.StartsWith("System.") &&
                              !line.Contains(" (at Assets/"))
                .ToList();

            // Write the filtered lines to the output file
            File.WriteAllLines(outputFilePath, filteredLines);

            Debug.Log($"Log entries have been successfully filtered and saved to {outputFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"An error occurred: {ex.Message}");
        }
    }
}
