using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PDFOrtnerSorter.Models;

public sealed partial class Job : ObservableObject
{
    /// <summary>
    /// Unique identifier for this job
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Name of the destination folder
    /// </summary>
    [ObservableProperty]
    private string _folderName = string.Empty;
    
    /// <summary>
    /// Full path to the destination folder
    /// </summary>
    [ObservableProperty]
    private string _destinationPath = string.Empty;
    
    /// <summary>
    /// List of files to be processed
    /// </summary>
    [ObservableProperty]
    private List<JobFileInfo> _files = new();
    
    /// <summary>
    /// Current status of the job
    /// </summary>
    [ObservableProperty]
    private JobStatus _status;
    
    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    [ObservableProperty]
    private double _progressPercentage;
    
    /// <summary>
    /// List of errors encountered during processing
    /// </summary>
    [ObservableProperty]
    private List<JobError> _errors = new();
    
    /// <summary>
    /// When the job was created
    /// </summary>
    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;
    
    /// <summary>
    /// When the job was completed (null if still running)
    /// </summary>
    [ObservableProperty]
    private DateTime? _completedAt;
    
    /// <summary>
    /// Total size of all files in bytes
    /// </summary>
    [ObservableProperty]
    private long _totalBytes;
    
    /// <summary>
    /// Number of bytes successfully transferred
    /// </summary>
    [ObservableProperty]
    private long _bytesTransferred;
    /// <summary>
    /// Number of files successfully processed
    /// </summary>
    [JsonIgnore]
    public int SuccessfulFileCount => Files.Count(f => f.IsSuccessful);
    
    /// <summary>
    /// Number of files that failed
    /// </summary>
    [JsonIgnore]
    public int FailedFileCount => Files.Count(f => !f.IsSuccessful && !string.IsNullOrEmpty(f.ErrorMessage));
    
    /// <summary>
    /// Total number of files
    /// </summary>
    [JsonIgnore]
    public int TotalFileCount => Files.Count;
    
    /// <summary>
    /// Whether the job has any errors
    /// </summary>
    [JsonIgnore]
    public bool HasErrors => Errors.Count > 0;
    
    /// <summary>
    /// Display name for the job
    /// </summary>
    [JsonIgnore]
    public string DisplayName => $"{FolderName} ({TotalFileCount} Dateien)";
}
