using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class SettingsDialogViewModel : ObservableObject
{
    private readonly IFolderPicker _folderPicker;
    private readonly ILabelPrintService _labelPrintService;

    public SettingsDialogViewModel(IFolderPicker folderPicker, ILabelPrintService labelPrintService)
    {
        _folderPicker = folderPicker;
        _labelPrintService = labelPrintService;
        
        // Load available printers using the service
        AvailablePrinters = new ObservableCollection<string>(
            _labelPrintService.GetAvailablePrinters()
        );
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(SourceFolder) || !string.IsNullOrWhiteSpace(DestinationBaseFolder);

    [ObservableProperty]
    private string? _sourceFolder;

    [ObservableProperty]
    private string? _destinationBaseFolder;

    [ObservableProperty]
    private string _destinationFolderName = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders;

    [ObservableProperty]
    private int _previewCacheLimitMb = 512;

    // Performance Options
    [ObservableProperty]
    private int _bufferSizeMB = 32;

    [ObservableProperty]
    private bool _dynamicConcurrencyEnabled = true;

    [ObservableProperty]
    private int _smallFileSizeThresholdMB = 100;

    [ObservableProperty]
    private bool _autoRefreshEnabled = false;

    [ObservableProperty]
    private bool _showMoveConfirmation = true;

    [ObservableProperty]
    private string _themeMode = "Light";

    [ObservableProperty]
    private bool _enableRollbackOnError = true;

    [ObservableProperty]
    private bool _showSlowTransferWarning = true;

    // Label Printing Options
    public ObservableCollection<string> AvailablePrinters { get; }
    
    public ObservableCollection<string> AvailableLabelFormats { get; } = new()
    {
        "Avery Zweckform 3474 (70x36mm)",
        "Brother DK-11208 (38x90mm)",
        "Dymo 99012 (36x89mm)"
    };

    [ObservableProperty]
    private string? _labelPrinterName;

    [ObservableProperty]
    private string _labelFormat = "Avery Zweckform 3474 (70x36mm)";

    [ObservableProperty]
    private bool _autoPrintLabelsEnabled = false;

    [ObservableProperty]
    private bool _includeBarcodeOnLabel = false;

    // System Options
    [ObservableProperty]
    private bool _startWithWindows = false;

    // Job Queue Options
    [ObservableProperty]
    private bool _autoRetryFailedFiles = true;

    [ObservableProperty]
    private int _maxRetries = 3;

    [ObservableProperty]
    private bool _showJobCompletionNotification = true;

    [ObservableProperty]
    private bool _autoClearCompletedJobs = false;

    [ObservableProperty]
    private int _autoClearCompletedJobsHours = 24;

    [ObservableProperty]
    private bool _enableJobPersistence = true;

    [ObservableProperty]
    private int _maxConcurrentJobs = 1;

    [RelayCommand]
    private void BrowseSourceFolder()
    {
        var selected = _folderPicker.PickFolder(SourceFolder, "Quellordner mit gescannten PDFs auswählen");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SourceFolder = selected;
        }
    }

    [RelayCommand]
    private void BrowseDestinationFolder()
    {
        var selected = _folderPicker.PickFolder(DestinationBaseFolder, "Zielbasisordner auswählen");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            DestinationBaseFolder = selected;
        }
    }

    public void ApplyFrom(SettingsDialogResult result)
    {
        SourceFolder = result.SourceFolder;
        DestinationBaseFolder = result.DestinationBaseFolder;
        DestinationFolderName = result.DestinationFolderName;
        IncludeSubfolders = result.IncludeSubfolders;
        PreviewCacheLimitMb = result.PreviewCacheLimitMb <= 0 ? 512 : result.PreviewCacheLimitMb;
    }

    public void ApplyFromAppSettings(AppSettings settings)
    {
        BufferSizeMB = settings.BufferSizeMB;
        DynamicConcurrencyEnabled = settings.DynamicConcurrencyEnabled;
        SmallFileSizeThresholdMB = settings.SmallFileSizeThresholdMB;
        AutoRefreshEnabled = settings.AutoRefreshEnabled;
        ShowMoveConfirmation = settings.ShowMoveConfirmation;
        ThemeMode = settings.ThemeMode;
        EnableRollbackOnError = settings.EnableRollbackOnError;
        ShowSlowTransferWarning = settings.ShowSlowTransferWarning;
        LabelPrinterName = settings.LabelPrinterName;
        LabelFormat = settings.LabelFormat;
        AutoPrintLabelsEnabled = settings.AutoPrintLabelsEnabled;
        IncludeBarcodeOnLabel = settings.IncludeBarcodeOnLabel;
        StartWithWindows = settings.StartWithWindows;
        AutoRetryFailedFiles = settings.AutoRetryFailedFiles;
        MaxRetries = settings.MaxRetries;
        ShowJobCompletionNotification = settings.ShowJobCompletionNotification;
        AutoClearCompletedJobs = settings.AutoClearCompletedJobs;
        AutoClearCompletedJobsHours = settings.AutoClearCompletedJobsHours;
        EnableJobPersistence = settings.EnableJobPersistence;
        MaxConcurrentJobs = settings.MaxConcurrentJobs;
    }

    public SettingsDialogResult ToResult()
    {
        var cacheLimit = PreviewCacheLimitMb <= 0 ? 512 : PreviewCacheLimitMb;
        var folderName = string.IsNullOrWhiteSpace(DestinationFolderName)
            ? $"Sortierung_{System.DateTime.Now:yyyyMMdd}"
            : DestinationFolderName.Trim();

        return new SettingsDialogResult(SourceFolder, DestinationBaseFolder, folderName, IncludeSubfolders, cacheLimit, ToAppSettings());
    }

    public AppSettings ToAppSettings()
    {
        return new AppSettings
        {
            LastSourceFolder = SourceFolder,
            LastDestinationFolder = DestinationBaseFolder,
            LastDestinationFolderName = DestinationFolderName,
            IncludeSubdirectories = IncludeSubfolders,
            PreviewCacheLimitMb = PreviewCacheLimitMb,
            BufferSizeMB = BufferSizeMB,
            DynamicConcurrencyEnabled = DynamicConcurrencyEnabled,
            SmallFileSizeThresholdMB = SmallFileSizeThresholdMB,
            AutoRefreshEnabled = AutoRefreshEnabled,
            ShowMoveConfirmation = ShowMoveConfirmation,
            ThemeMode = ThemeMode,
            EnableRollbackOnError = EnableRollbackOnError,
            ShowSlowTransferWarning = ShowSlowTransferWarning,
            LabelPrinterName = LabelPrinterName,
            LabelFormat = LabelFormat,
            AutoPrintLabelsEnabled = AutoPrintLabelsEnabled,
            IncludeBarcodeOnLabel = IncludeBarcodeOnLabel,
            StartWithWindows = StartWithWindows,
            AutoRetryFailedFiles = AutoRetryFailedFiles,
            MaxRetries = MaxRetries,
            ShowJobCompletionNotification = ShowJobCompletionNotification,
            AutoClearCompletedJobs = AutoClearCompletedJobs,
            AutoClearCompletedJobsHours = AutoClearCompletedJobsHours,
            EnableJobPersistence = EnableJobPersistence,
            MaxConcurrentJobs = MaxConcurrentJobs
        };
    }

    partial void OnSourceFolderChanged(string? value) => OnPropertyChanged(nameof(CanSave));
    partial void OnDestinationBaseFolderChanged(string? value) => OnPropertyChanged(nameof(CanSave));
}
