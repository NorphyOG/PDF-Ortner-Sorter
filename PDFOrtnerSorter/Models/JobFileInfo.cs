namespace PDFOrtnerSorter.Models;

public sealed class JobFileInfo
{
    /// <summary>
    /// Name of the file
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full source path
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long SizeBytes { get; set; }
    
    /// <summary>
    /// Whether this file was successfully processed
    /// </summary>
    public bool IsSuccessful { get; set; }
    
    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
