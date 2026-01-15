using System.Text.Json.Serialization;

namespace PDFOrtnerSorter.Models;

public sealed class AppSettings
{
    public string? LastSourceFolder { get; set; }
    public string? LastDestinationFolder { get; set; }
    public string? LastDestinationFolderName { get; set; }
    public bool IncludeSubdirectories { get; set; } = true;
    public int PreviewCacheLimitMb { get; set; } = 512;

    // Performance Options
    public int BufferSizeMB { get; set; } = 32;
    public bool DynamicConcurrencyEnabled { get; set; } = true;
    public int SmallFileSizeThresholdMB { get; set; } = 100;
    public bool AutoRefreshEnabled { get; set; } = false;
    public bool ShowMoveConfirmation { get; set; } = true;
    public string ThemeMode { get; set; } = "Light";
    public bool EnableRollbackOnError { get; set; } = true;
    public bool ShowSlowTransferWarning { get; set; } = true;

    // Label Printing Options
    public string? LabelPrinterName { get; set; }
    public string LabelFormat { get; set; } = "Avery Zweckform 3474 (70x36mm)";
    public bool AutoPrintLabelsEnabled { get; set; } = false;
    public bool IncludeBarcodeOnLabel { get; set; } = false;

    // System Options
    public bool StartWithWindows { get; set; } = false;
    public bool SoundNotificationsEnabled { get; set; } = true;

    // Job Queue Options
    public bool AutoRetryFailedFiles { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public bool ShowJobCompletionNotification { get; set; } = true;
    public bool AutoClearCompletedJobs { get; set; } = false;
    public int AutoClearCompletedJobsHours { get; set; } = 24;
    public bool EnableJobPersistence { get; set; } = true;
    public int MaxConcurrentJobs { get; set; } = 1;

    [JsonIgnore]
    public static AppSettings Default => new();
}
