using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ILoggerService
{
    string LogFilePath { get; }
    void LogInfo(string message);
    void LogError(string message, Exception? exception = null);
    void LogTransferStatistics(MoveBatchResult result, TimeSpan duration, double averageSpeedMBps, double peakSpeedMBps);
}
