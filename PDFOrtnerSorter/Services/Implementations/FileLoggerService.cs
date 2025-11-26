using System.IO;
using System.Text;
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

    private void WriteLine(string content)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {content}";
        lock (_syncRoot)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _writer?.Dispose();
        }
    }
}
