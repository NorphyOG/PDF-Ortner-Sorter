using System.IO;
using System.Text;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class FileLoggerService : ILoggerService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly StreamWriter _writer;
    public string LogFilePath { get; }

    public FileLoggerService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFOrtnerSorter", "Logs");
        Directory.CreateDirectory(root);
        LogFilePath = Path.Combine(root, "app.log");
        
        // Open with FileShare.ReadWrite to avoid locking issues
        var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        
        WriteLine("=== Application start ===");
    }

    public void LogInfo(string message) => WriteLine($"INFO  {message}");

    public void LogError(string message, Exception? exception = null)
    {
        var builder = new StringBuilder();
        builder.Append("ERROR ").Append(message);
        if (exception is not null)
        {
            builder.AppendLine().Append(exception);
        }

        WriteLine(builder.ToString());
    }

    public void LogTransferStatistics(
        MoveBatchResult result,
        TimeSpan duration,
        double averageSpeedMBps,
        double peakSpeedMBps)
    {
        var successRate = result.RequestedCount > 0 ? (result.SuccessCount * 100.0 / result.RequestedCount) : 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== Transfer Statistics ===");
        builder.AppendLine($"Duration: {duration:hh\\:mm\\:ss}");
        builder.AppendLine($"Total Files: {result.RequestedCount}");
        builder.AppendLine($"Successful: {result.SuccessCount}");
        builder.AppendLine($"Failed: {result.Failures.Count}");
        builder.AppendLine($"Success Rate: {successRate:F1}%");
        builder.AppendLine($"Total Bytes: {FormatBytes(result.BytesTransferred)}");
        builder.AppendLine($"Average Speed: {averageSpeedMBps:F1} MB/s");
        builder.AppendLine($"Peak Speed: {peakSpeedMBps:F1} MB/s");
        
        if (result.BackupDirectory != null)
        {
            builder.AppendLine($"Rollback Backup: {result.BackupDirectory}");
        }

        if (result.Failures.Count > 0)
        {
            builder.AppendLine("Failures:");
            foreach (var failure in result.Failures)
            {
                builder.AppendLine($"  - {failure.SourcePath}: {failure.Reason}");
            }
        }

        WriteLine(builder.ToString());
    }

    private void WriteLine(string content)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {content}";
        lock (_syncRoot)
        {
            _writer.WriteLine(line);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _writer?.Dispose();
        }
    }
}
