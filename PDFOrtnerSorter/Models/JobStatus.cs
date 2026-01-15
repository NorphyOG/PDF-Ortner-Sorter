namespace PDFOrtnerSorter.Models;

public enum JobStatus
{
    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Running,
    
    /// <summary>
    /// Job has completed (may include partial failures)
    /// </summary>
    Completed
}
