using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IJobStore
{
    /// <summary>
    /// Loads all jobs from storage
    /// </summary>
    Task<List<Job>> LoadJobsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Saves a job to storage
    /// </summary>
    Task SaveJobAsync(Job job, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets completed jobs with pagination
    /// </summary>
    Task<List<Job>> GetCompletedJobsAsync(int skip, int take, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets total count of completed jobs
    /// </summary>
    Task<int> GetCompletedJobCountAsync(CancellationToken cancellationToken = default);
}
