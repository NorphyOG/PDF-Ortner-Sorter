using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IJobQueueService
{
    /// <summary>
    /// Event fired when a job completes
    /// </summary>
    event EventHandler<Job>? JobCompleted;
    
    /// <summary>
    /// Event fired when a job's progress updates
    /// </summary>
    event EventHandler<Job>? JobProgressUpdated;
    
    /// <summary>
    /// Adds a new job to the queue
    /// </summary>
    Task<Job> EnqueueJobAsync(string folderName, string destinationBase, IEnumerable<PdfDocumentInfo> documents, CancellationToken cancellationToken);
    
    /// <summary>
    /// Gets the currently running job
    /// </summary>
    Job? GetCurrentJob();
    
    /// <summary>
    /// Gets all queued jobs (waiting to run)
    /// </summary>
    IReadOnlyList<Job> GetQueuedJobs();
    
    /// <summary>
    /// Retries a failed job (re-processes only failed files)
    /// </summary>
    Task RetryJobAsync(Job job, CancellationToken cancellationToken);
    
    /// <summary>
    /// Starts processing the queue (should be called once at startup)
    /// </summary>
    Task StartProcessingAsync(CancellationToken cancellationToken);
}
