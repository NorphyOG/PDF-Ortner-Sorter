using System.IO;
using System.Text.Json;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class JobStore : IJobStore
{
    private readonly string _storageFolder;
    private readonly string _storageFile;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private List<Job> _cachedJobs = new();
    private bool _isLoaded;

    public JobStore()
    {
        _storageFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PDFOrtnerSorter");
        
        _storageFile = Path.Combine(_storageFolder, "jobs.json");
        
        Directory.CreateDirectory(_storageFolder);
    }

    public async Task<List<Job>> LoadJobsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        
        try
        {
            if (_isLoaded)
            {
                return _cachedJobs;
            }

            if (!File.Exists(_storageFile))
            {
                _cachedJobs = new List<Job>();
                _isLoaded = true;
                return _cachedJobs;
            }

            var json = await File.ReadAllTextAsync(_storageFile, cancellationToken);
            _cachedJobs = JsonSerializer.Deserialize<List<Job>>(json) ?? new List<Job>();

            // Fix any running jobs (app was closed while processing)
            foreach (var job in _cachedJobs.Where(j => j.Status == JobStatus.Running))
            {
                job.Status = JobStatus.Completed;
                job.CompletedAt = job.CompletedAt ?? DateTime.UtcNow;
                
                // Mark as interrupted
                if (!job.Errors.Any(e => e.ErrorType == JobErrorType.Unknown && e.FileName == "Interrupted"))
                {
                    job.Errors.Add(new JobError
                    {
                        FileName = "Interrupted",
                        FilePath = string.Empty,
                        ErrorType = JobErrorType.Unknown,
                        ErrorMessage = "Job was interrupted when application closed",
                        RetryCount = 0,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            _isLoaded = true;
            return _cachedJobs;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        
        try
        {
            // Ensure jobs are loaded
            if (!_isLoaded)
            {
                await LoadJobsAsync(cancellationToken);
            }

            // Update or add job
            var existingJob = _cachedJobs.FirstOrDefault(j => j.Id == job.Id);
            if (existingJob != null)
            {
                _cachedJobs.Remove(existingJob);
            }
            
            _cachedJobs.Add(job);

            // Save to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(_cachedJobs, options);
            await File.WriteAllTextAsync(_storageFile, json, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<List<Job>> GetCompletedJobsAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        
        try
        {
            if (!_isLoaded)
            {
                await LoadJobsAsync(cancellationToken);
            }

            return _cachedJobs
                .Where(j => j.Status == JobStatus.Completed)
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<int> GetCompletedJobCountAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        
        try
        {
            if (!_isLoaded)
            {
                await LoadJobsAsync(cancellationToken);
            }

            return _cachedJobs.Count(j => j.Status == JobStatus.Completed);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
