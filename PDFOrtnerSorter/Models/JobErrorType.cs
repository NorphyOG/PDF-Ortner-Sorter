namespace PDFOrtnerSorter.Models;

public enum JobErrorType
{
    /// <summary>
    /// Temporary error that may succeed on retry (e.g., file locked)
    /// </summary>
    Transient,
    
    /// <summary>
    /// Insufficient disk space
    /// </summary>
    DiskSpace,
    
    /// <summary>
    /// Permission denied
    /// </summary>
    Permissions,
    
    /// <summary>
    /// File not found or path too long
    /// </summary>
    FileSystem,
    
    /// <summary>
    /// Other unknown errors
    /// </summary>
    Unknown
}
