using System.Collections.Concurrent;
using System.IO;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class JobQueueService : IJobQueueService
{
    private readonly IMoveService _moveService;
    private readonly ILoggerService _logger;
    private readonly ConcurrentQueue<Job> _jobQueue = new();
    private Job? _currentJob;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private bool _isProcessing;

    public event EventHandler<Job>? JobCompleted;
    public event EventHandler<Job>? JobProgressUpdated;

    public JobQueueService(IMoveService moveService, ILoggerService logger)
    {
        _moveService = moveService;
        _logger = logger;
    }

    public async Task<Job> EnqueueJobAsync(
        string folderName, 
        string destinationBase, 
        IEnumerable<PdfDocumentInfo> documents, 
        CancellationToken cancellationToken)
    {
        var documentsList = documents.ToList();
        
        var job = new Job
        {
            FolderName = folderName,
            DestinationPath = Path.Combine(destinationBase, folderName),
            Status = JobStatus.Running,
            Files = documentsList.Select(d => new JobFileInfo
            {
                FileName = d.FileName,
                SourcePath = d.FullPath,
                SizeBytes = d.Length,
                IsSuccessful = false
            }).ToList(),
            TotalBytes = documentsList.Sum(d => d.Length)
        };

        _jobQueue.Enqueue(job);
        _logger.LogInfo($"Job enqueued: {job.DisplayName}");

        return job;
    }

    public Job? GetCurrentJob() => _currentJob;

    public IReadOnlyList<Job> GetQueuedJobs() => _jobQueue.ToList();

    public async Task RetryJobAsync(Job job, CancellationToken cancellationToken)
    {
        // Reset failed files
        foreach (var file in job.Files.Where(f => !f.IsSuccessful))
        {
            file.IsSuccessful = false;
            file.ErrorMessage = null;
        }

        // Remove old errors
        var failedFileNames = job.Files.Where(f => !f.IsSuccessful).Select(f => f.FileName).ToHashSet();
        job.Errors.RemoveAll(e => failedFileNames.Contains(e.FileName));

        // Reset job status
        job.Status = JobStatus.Running;
        job.CompletedAt = null;
        job.ProgressPercentage = 0;

        // Re-enqueue
        _jobQueue.Enqueue(job);
        _logger.LogInfo($"Job retrying: {job.DisplayName}");
    }

    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        if (_isProcessing)
        {
            return;
        }

        _isProcessing = true;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_jobQueue.TryDequeue(out var job))
                {
                    await ProcessJobAsync(job, cancellationToken);
                }
                else
                {
                    // Wait a bit before checking again
                    await Task.Delay(500, cancellationToken);
                }
            }
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private async Task ProcessJobAsync(Job job, CancellationToken cancellationToken)
    {
        await _processingLock.WaitAsync(cancellationToken);
        
        try
        {
            _currentJob = job;
            _logger.LogInfo($"Processing job: {job.DisplayName}");

            // Get documents to process (only those not yet successful)
            var documentsToProcess = job.Files
                .Where(f => !f.IsSuccessful)
                .Select(f => new PdfDocumentInfo
                {
                    FileName = f.FileName,
                    FullPath = f.SourcePath,
                    Length = f.SizeBytes,
                    LastWriteTimeUtc = DateTime.UtcNow // Not critical for move operation
                })
                .ToList();

            if (documentsToProcess.Count == 0)
            {
                // All files already processed
                CompleteJob(job);
                return;
            }

            var progress = new Progress<DetailedMoveProgress>(p =>
            {
                job.ProgressPercentage = p.TotalBytes > 0 
                    ? (double)p.BytesTransferred / p.TotalBytes * 100 
                    : 0;
                job.BytesTransferred = p.BytesTransferred;
                
                JobProgressUpdated?.Invoke(this, job);
            });

            // Ensure destination folder exists
            var destinationBase = Path.GetDirectoryName(job.DestinationPath) 
                ?? throw new InvalidOperationException("Invalid destination path");

            var result = await _moveService.MoveAsync(
                documentsToProcess,
                destinationBase,
                job.FolderName,
                progress,
                cancellationToken);

            // Update job with results
            foreach (var file in job.Files)
            {
                var wasProcessed = documentsToProcess.Any(d => d.FullPath == file.SourcePath);
                if (!wasProcessed)
                {
                    continue; // Skip already successful files
                }

                var failure = result.Failures.FirstOrDefault(f => f.SourcePath == file.SourcePath);
                if (failure != null)
                {
                    file.IsSuccessful = false;
                    file.ErrorMessage = failure.Reason;

                    // Classify error type
                    var errorType = ClassifyError(failure.Reason);
                    
                    var existingError = job.Errors.FirstOrDefault(e => e.FilePath == file.SourcePath);
                    if (existingError != null)
                    {
                        existingError.RetryCount++;
                        existingError.Timestamp = DateTime.UtcNow;
                        existingError.ErrorMessage = failure.Reason;
                    }
                    else
                    {
                        job.Errors.Add(new JobError
                        {
                            FileName = file.FileName,
                            FilePath = file.SourcePath,
                            ErrorType = errorType,
                            ErrorMessage = failure.Reason,
                            RetryCount = 0,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    file.IsSuccessful = true;
                    file.ErrorMessage = null;
                }
            }

            job.BytesTransferred = result.BytesTransferred;
            CompleteJob(job);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Job processing failed: {job.DisplayName}", ex);
            
            // Mark job as completed with error
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ProgressPercentage = 100;

            // Add general error
            job.Errors.Add(new JobError
            {
                FileName = "General",
                FilePath = string.Empty,
                ErrorType = JobErrorType.Unknown,
                ErrorMessage = ex.Message,
                RetryCount = 0,
                Timestamp = DateTime.UtcNow
            });

            JobCompleted?.Invoke(this, job);
        }
        finally
        {
            _currentJob = null;
            _processingLock.Release();
        }
    }

    private void CompleteJob(Job job)
    {
        job.Status = JobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.ProgressPercentage = 100;

        _logger.LogInfo($"Job completed: {job.DisplayName} - Success: {job.SuccessfulFileCount}/{job.TotalFileCount}");
        
        JobCompleted?.Invoke(this, job);
    }

    private JobErrorType ClassifyError(string errorMessage)
    {
        var lower = errorMessage.ToLowerInvariant();

        if (lower.Contains("being used by another process") ||
            lower.Contains("locked") ||
            lower.Contains("access denied"))
        {
            return JobErrorType.Transient;
        }

        if (lower.Contains("disk") && lower.Contains("space") ||
            lower.Contains("not enough space"))
        {
            return JobErrorType.DiskSpace;
        }

        if (lower.Contains("permission") ||
            lower.Contains("unauthorized") ||
            lower.Contains("access is denied"))
        {
            return JobErrorType.Permissions;
        }

        if (lower.Contains("not found") ||
            lower.Contains("path too long") ||
            lower.Contains("invalid path"))
        {
            return JobErrorType.FileSystem;
        }

        return JobErrorType.Unknown;
    }
}
