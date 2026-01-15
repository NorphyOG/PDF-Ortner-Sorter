namespace PDFOrtnerSorter.Models;

public sealed class JobError
{
    /// <summary>
    /// Name of the file that encountered an error
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full path of the file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of error encountered
    /// </summary>
    public JobErrorType ErrorType { get; set; }
    
    /// <summary>
    /// Detailed error message
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of times this file has been retried
    /// </summary>
    public int RetryCount { get; set; }
    
    /// <summary>
    /// When the error occurred
    /// </summary>
    public DateTime Timestamp { get; set; }
}
