using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Dialogs;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IPreviewService _previewService;
    private readonly IJobQueueService _jobQueueService;
    private readonly IJobStore _jobStore;
    private readonly ISettingsService _settingsService;
    private readonly ICatalogStore _catalogStore;
    private readonly IFolderPicker _folderPicker;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IMoveConfirmationService _moveConfirmationService;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly ILabelPrintDialogService _labelPrintDialogService;
    private readonly IAutostartService _autostartService;
    private readonly ILoggerService _logger;

    private readonly ObservableCollection<PdfDocumentViewModel> _documents = new();
    private readonly ObservableCollection<Job> _runningJobs = new();
    private readonly ObservableCollection<Job> _completedJobs = new();
    private readonly ObservableCollection<string> _folderNameSuggestions = new();
    
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _processingCts;
    private bool _suspendSettingsPropagation;
    private int _completedJobsLoaded = 0;
    private const int JobsPerPage = 10;

    public MainViewModel(
        IFileService fileService,
        IPreviewService previewService,
        IJobQueueService jobQueueService,
        IJobStore jobStore,
        ISettingsService settingsService,
        ICatalogStore catalogStore,
        IFolderPicker folderPicker,
        IBackgroundJobQueue backgroundJobQueue,
        IMoveConfirmationService moveConfirmationService,
        ISettingsDialogService settingsDialogService,
        ILabelPrintDialogService labelPrintDialogService,
        IAutostartService autostartService,
        ILoggerService logger)
    {
        _fileService = fileService;
        _previewService = previewService;
        _jobQueueService = jobQueueService;
        _jobStore = jobStore;
        _settingsService = settingsService;
        _catalogStore = catalogStore;
        _folderPicker = folderPicker;
        _backgroundJobQueue = backgroundJobQueue;
        _moveConfirmationService = moveConfirmationService;
        _settingsDialogService = settingsDialogService;
        _labelPrintDialogService = labelPrintDialogService;
        _autostartService = autostartService;
        _logger = logger;

        Documents = new ReadOnlyObservableCollection<PdfDocumentViewModel>(_documents);
        RunningJobs = new ReadOnlyObservableCollection<Job>(_runningJobs);
        CompletedJobs = new ReadOnlyObservableCollection<Job>(_completedJobs);
        FolderNameSuggestions = new ReadOnlyObservableCollection<string>(_folderNameSuggestions);
        
        DestinationFolderName = $"Sortierung_{DateTime.Now:yyyyMMdd}";
        
        // Subscribe to job queue events
        _jobQueueService.JobCompleted += OnJobCompleted;
        _jobQueueService.JobProgressUpdated += OnJobProgressUpdated;
    }

    public ReadOnlyObservableCollection<PdfDocumentViewModel> Documents { get; }
    public ReadOnlyObservableCollection<Job> RunningJobs { get; }
    public ReadOnlyObservableCollection<Job> CompletedJobs { get; }
    public ReadOnlyObservableCollection<string> FolderNameSuggestions { get; }

    [ObservableProperty]
    private int _documentsCount = 0;

    [ObservableProperty]
    private int _selectedCount = 0;
    
    public bool HasMoreCompletedJobs => _completedJobsLoaded < TotalCompletedJobCount;
    
    public int TotalCompletedJobCount { get; private set; }

    [ObservableProperty]
    private string? _sourceFolder;

    [ObservableProperty]
    private string? _destinationBaseFolder;

    [ObservableProperty]
    private string _destinationFolderName = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _previewCacheLimitMb = 512;

    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0 = Running, 1 = Completed

    [ObservableProperty]
    private bool _soundNotificationsEnabled = true;

    partial void OnDestinationFolderNameChanged(string value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        // Update suggestions
        UpdateFolderNameSuggestions(value);
        SafePersistSettingsAsync();
    }

    private void UpdateDocumentCounts()
    {
        DocumentsCount = _documents.Count;
        SelectedCount = _documents.Count(d => d.IsSelected);
    }

    private void UpdateFolderNameSuggestions(string input)
    {
        var allFolderNames = _completedJobs
            .Select(j => j.FolderName)
            .Union(_runningJobs.Select(j => j.FolderName))
            .ToList();

        if (allFolderNames.Count == 0)
        {
            _folderNameSuggestions.Clear();
            return;
        }

        var suggestions = Infrastructure.FuzzyMatcher.FindSimilar(input, allFolderNames, maxResults: 8);
        
        _ = RunOnUiAsync(() =>
        {
            _folderNameSuggestions.Clear();
            foreach (var suggestion in suggestions)
            {
                _folderNameSuggestions.Add(suggestion);
            }
        });
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        _suspendSettingsPropagation = true;
        try
        {
            SourceFolder = settings.LastSourceFolder;
            DestinationBaseFolder = settings.LastDestinationFolder;
            DestinationFolderName = string.IsNullOrWhiteSpace(settings.LastDestinationFolderName)
                ? DestinationFolderName
                : settings.LastDestinationFolderName;
            IncludeSubfolders = settings.IncludeSubdirectories;
            PreviewCacheLimitMb = settings.PreviewCacheLimitMb <= 0 ? 512 : settings.PreviewCacheLimitMb;
            SoundNotificationsEnabled = settings.SoundNotificationsEnabled;
        }
        finally
        {
            _suspendSettingsPropagation = false;
        }

        _previewService.ConfigureCacheLimit(PreviewCacheLimitMb);

        // Load jobs from store
        await LoadInitialJobsAsync(cancellationToken);

        // Start job queue processing
        _processingCts = new CancellationTokenSource();
        _ = _jobQueueService.StartProcessingAsync(_processingCts.Token);

        if (!string.IsNullOrWhiteSpace(SourceFolder))
        {
            await LoadDocumentsAsync(cancellationToken);
        }
    }
    
    private async Task LoadInitialJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await _jobStore.LoadJobsAsync(cancellationToken);
        
        // Populate running jobs (should be empty after app start due to status fixup in JobStore)
        await RunOnUiAsync(() =>
        {
            _runningJobs.Clear();
            foreach (var job in jobs.Where(j => j.Status == JobStatus.Running))
            {
                _runningJobs.Add(job);
            }
        });

        // Load first page of completed jobs
        TotalCompletedJobCount = await _jobStore.GetCompletedJobCountAsync(cancellationToken);
        await LoadMoreCompletedJobsAsync(cancellationToken);
        
        // Update folder name suggestions
        UpdateFolderNameSuggestions(DestinationFolderName);
    }

    [RelayCommand]
    private async Task BrowseSourceAsync()
    {
        var selected = _folderPicker.PickFolder(SourceFolder, "Quellordner mit gescannten PDFs auswählen");
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        SourceFolder = selected;
        _logger.LogInfo($"Source folder selected: {SourceFolder}");
        await PersistSettingsAsync(CancellationToken.None);
        await LoadDocumentsAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task BrowseDestinationAsync()
    {
        var selected = _folderPicker.PickFolder(DestinationBaseFolder, "Zielbasisordner auswählen");
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        DestinationBaseFolder = selected;
        _logger.LogInfo($"Destination base folder selected: {DestinationBaseFolder}");
        await PersistSettingsAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var appSettings = await _settingsService.LoadAsync(CancellationToken.None);
        
        var current = new SettingsDialogResult(
            SourceFolder,
            DestinationBaseFolder,
            string.IsNullOrWhiteSpace(DestinationFolderName)
                ? $"Sortierung_{DateTime.Now:yyyyMMdd}"
                : DestinationFolderName,
            IncludeSubfolders,
            PreviewCacheLimitMb,
            appSettings);

        var result = await _settingsDialogService.ShowAsync(current, appSettings);
        if (result is null)
        {
            return;
        }

        // Check if StartWithWindows setting changed
        var oldStartWithWindows = appSettings.StartWithWindows;
        var newAppSettings = result.Settings;
        
        var shouldReload = !string.Equals(SourceFolder, result.SourceFolder, StringComparison.OrdinalIgnoreCase)
                           || IncludeSubfolders != result.IncludeSubfolders;

        _suspendSettingsPropagation = true;
        try
        {
            SourceFolder = result.SourceFolder;
            DestinationBaseFolder = result.DestinationBaseFolder;
            DestinationFolderName = result.DestinationFolderName;
            IncludeSubfolders = result.IncludeSubfolders;
            PreviewCacheLimitMb = result.PreviewCacheLimitMb;
        }
        finally
        {
            _suspendSettingsPropagation = false;
        }

        _previewService.ConfigureCacheLimit(PreviewCacheLimitMb);
        
        // Save all settings (including those that aren't MainViewModel properties)
        await _settingsService.SaveAsync(newAppSettings, CancellationToken.None);

        // Apply autostart setting if changed
        if (oldStartWithWindows != newAppSettings.StartWithWindows)
        {
            try
            {
                if (newAppSettings.StartWithWindows)
                {
                    await _autostartService.EnableAsync();
                    _logger.LogInfo("Autostart wurde aktiviert");
                }
                else
                {
                    await _autostartService.DisableAsync();
                    _logger.LogInfo("Autostart wurde deaktiviert");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Fehler beim Ändern der Autostart-Einstellung", ex);
                MessageBox.Show(
                    "Die Autostart-Einstellung konnte nicht geändert werden. Überprüfen Sie die Berechtigungen.",
                    "Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        if (shouldReload && !string.IsNullOrWhiteSpace(SourceFolder))
        {
            await LoadDocumentsAsync(CancellationToken.None);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        await LoadDocumentsAsync(CancellationToken.None);
    }

    private bool CanRefresh() => !IsBusy && !string.IsNullOrWhiteSpace(SourceFolder);

    [RelayCommand(CanExecute = nameof(CanCreateJob))]
    private async Task CreateJobAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationBaseFolder))
        {
            StatusMessage = "Bitte Zielordner wählen.";
            return;
        }

        var selected = _documents.Where(d => d.IsSelected).Select(d => d.Document).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Keine Dateien ausgewählt.";
            return;
        }

        var confirmedFolderName = await _moveConfirmationService.ConfirmDestinationFolderAsync(DestinationFolderName);
        if (confirmedFolderName is null)
        {
            StatusMessage = "Job-Erstellung abgebrochen.";
            return;
        }

        if (!string.Equals(DestinationFolderName, confirmedFolderName, StringComparison.Ordinal))
        {
            DestinationFolderName = confirmedFolderName;
            await PersistSettingsAsync(CancellationToken.None);
        }

        _logger.LogInfo($"Creating job for {selected.Count} files -> {DestinationBaseFolder}\\{DestinationFolderName}");

        try
        {
            var job = await _jobQueueService.EnqueueJobAsync(
                DestinationFolderName,
                DestinationBaseFolder!,
                selected,
                CancellationToken.None);

            // Add to running jobs UI
            await RunOnUiAsync(() =>
            {
                if (!_runningJobs.Any(j => j.Id == job.Id))
                {
                    _runningJobs.Add(job);
                }
            });

            // Save job
            await _jobStore.SaveJobAsync(job, CancellationToken.None);

            StatusMessage = $"Job erstellt: {job.DisplayName}";
            
            // Switch to Running tab
            SelectedTabIndex = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Erstellen des Jobs: {ex.Message}";
            _logger.LogError("CreateJobAsync failed", ex);
        }
    }

    private bool CanCreateJob()
    {
        return !IsBusy
               && !string.IsNullOrWhiteSpace(DestinationBaseFolder)
               && _documents.Any(d => d.IsSelected);
    }

    [RelayCommand]
    private void SelectAllDocuments()
    {
        foreach (var document in _documents)
        {
            document.IsSelected = true;
        }

        CreateJobCommand.NotifyCanExecuteChanged();
        UpdateDocumentCounts();
    }

    [RelayCommand]
    private void ToggleFileSelection(PdfDocumentViewModel? document)
    {
        if (document == null) return;
        document.IsSelected = !document.IsSelected;
        CreateJobCommand.NotifyCanExecuteChanged();
        UpdateDocumentCounts();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var document in _documents)
        {
            document.IsSelected = false;
        }

        CreateJobCommand.NotifyCanExecuteChanged();
        UpdateDocumentCounts();
    }
    
    [RelayCommand]
    private async Task LoadMoreJobsAsync()
    {
        await LoadMoreCompletedJobsAsync(CancellationToken.None);
    }
    
    private async Task LoadMoreCompletedJobsAsync(CancellationToken cancellationToken)
    {
        var jobs = await _jobStore.GetCompletedJobsAsync(_completedJobsLoaded, JobsPerPage, cancellationToken);
        
        await RunOnUiAsync(() =>
        {
            foreach (var job in jobs)
            {
                if (!_completedJobs.Any(j => j.Id == job.Id))
                {
                    _completedJobs.Add(job);
                }
            }
        });

        _completedJobsLoaded += jobs.Count;
        OnPropertyChanged(nameof(HasMoreCompletedJobs));
    }
    
    [RelayCommand]
    private async Task ShowJobDetailsAsync(Job? job)
    {
        if (job == null) return;

        var viewModel = new JobDetailsViewModel(job);
        viewModel.RetryRequested += async (_, retryJob) =>
        {
            await RetryJobAsync(retryJob);
        };

        var window = new JobDetailsWindow(viewModel)
        {
            Owner = Application.Current.MainWindow
        };
        
        window.ShowDialog();
    }
    
    [RelayCommand]
    private async Task RetryJobAsync(Job job)
    {
        try
        {
            _logger.LogInfo($"Retrying job: {job.DisplayName}");

            await _jobQueueService.RetryJobAsync(job, CancellationToken.None);

            // Move from completed to running
            await RunOnUiAsync(() =>
            {
                _completedJobs.Remove(job);
                
                if (!_runningJobs.Any(j => j.Id == job.Id))
                {
                    _runningJobs.Add(job);
                }
            });

            // Save updated job
            await _jobStore.SaveJobAsync(job, CancellationToken.None);

            StatusMessage = $"Job wird wiederholt: {job.DisplayName}";
            SelectedTabIndex = 0; // Switch to Running tab
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler beim Wiederholen: {ex.Message}";
            _logger.LogError("RetryJobAsync failed", ex);
        }
    }
    
    private void OnJobCompleted(object? sender, Job job)
    {
        _ = RunOnUiAsync(async () =>
        {
            // Move from running to completed
            _runningJobs.Remove(job);
            
            // Insert at beginning of completed jobs
            _completedJobs.Insert(0, job);
            _completedJobsLoaded++;

            // Save to store
            await _jobStore.SaveJobAsync(job, CancellationToken.None);
            
            // Update count
            TotalCompletedJobCount = await _jobStore.GetCompletedJobCountAsync(CancellationToken.None);
            OnPropertyChanged(nameof(HasMoreCompletedJobs));

            // Update folder name suggestions
            UpdateFolderNameSuggestions(DestinationFolderName);

            // Play completion sound
            if (SoundNotificationsEnabled)
            {
                PlayCompletionSound();
            }

            // Update status
            if (job.HasErrors)
            {
                StatusMessage = $"Job abgeschlossen mit {job.FailedFileCount} Fehler(n): {job.DisplayName}";
            }
            else
            {
                StatusMessage = $"Job erfolgreich abgeschlossen: {job.DisplayName}";
            }
            
            _logger.LogInfo($"Job completed: {job.DisplayName} - Success: {job.SuccessfulFileCount}/{job.TotalFileCount}");
        });
    }
    
    private void OnJobProgressUpdated(object? sender, Job job)
    {
        _ = RunOnUiAsync(() =>
        {
            // Job is now ObservableObject, so property changes are automatically bound
            // The UI will update in real-time as properties change on the job object
        });
    }
    
    private void PlayCompletionSound()
    {
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to play completion sound", ex);
        }
    }

    private async Task LoadDocumentsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SourceFolder))
        {
            await RunOnUiAsync(() =>
            {
                foreach (var existing in _documents)
                {
                    existing.PropertyChanged -= OnDocumentPropertyChanged;
                }

                _documents.Clear();
                StatusMessage = "Kein Quellordner ausgewählt.";
                OnPropertyChanged(nameof(SelectedCount));
            });
            return;
        }

        _loadCts?.Cancel();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await RunOnUiAsync(() =>
        {
            IsBusy = true;
            StatusMessage = "Scanne PDFs...";

            foreach (var existing in _documents)
            {
                existing.PropertyChanged -= OnDocumentPropertyChanged;
            }

            _documents.Clear();
            UpdateDocumentCounts();
        });

        var entries = new List<CatalogEntry>();

        try
        {
            await foreach (var info in _fileService.EnumerateAsync(SourceFolder, IncludeSubfolders, _loadCts.Token).ConfigureAwait(false))
            {
                var viewModel = new PdfDocumentViewModel(info, _previewService, _backgroundJobQueue);

                await RunOnUiAsync(() =>
                {
                    viewModel.PropertyChanged += OnDocumentPropertyChanged;
                    _documents.Add(viewModel);
                    UpdateDocumentCounts();
                });

                viewModel.EnsurePreview(_loadCts.Token);

                entries.Add(new CatalogEntry
                {
                    Path = info.FullPath,
                    Length = info.Length,
                    LastWriteTimeUtc = info.LastWriteTimeUtc,
                    IndexedAtUtc = DateTime.UtcNow
                });
            }

            await _catalogStore.SaveSnapshotAsync(entries, cancellationToken).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                StatusMessage = _documents.Count == 0 ? "Keine PDFs gefunden." : $"{_documents.Count} PDFs geladen.";
            });
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() => StatusMessage = "Ladevorgang abgebrochen.");
            _logger.LogInfo("Ladevorgang abgebrochen.");
        }
        catch (Exception ex)
        {
            await RunOnUiAsync(() => StatusMessage = $"Fehler beim Laden: {ex.Message}");
            _logger.LogError("Fehler beim Laden", ex);
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                IsBusy = false;
                RefreshCommand.NotifyCanExecuteChanged();
                CreateJobCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedCount));
            });
        }
    }

    private static Task RunOnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return Task.CompletedTask;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private async Task PersistSettingsAsync(CancellationToken cancellationToken)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        // Load current settings to preserve values we don't have in MainViewModel
        var currentSettings = await _settingsService.LoadAsync(cancellationToken);
        
        // Update only the fields we manage in MainViewModel
        currentSettings.LastSourceFolder = SourceFolder;
        currentSettings.LastDestinationFolder = DestinationBaseFolder;
        currentSettings.LastDestinationFolderName = DestinationFolderName;
        currentSettings.IncludeSubdirectories = IncludeSubfolders;
        currentSettings.PreviewCacheLimitMb = PreviewCacheLimitMb;

        await _settingsService.SaveAsync(currentSettings, cancellationToken);
    }

    /// <summary>
    /// Safe wrapper for fire-and-forget PersistSettingsAsync to prevent unobserved exceptions
    /// </summary>
    private async void SafePersistSettingsAsync()
    {
        try
        {
            await PersistSettingsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception in SafePersistSettingsAsync", ex);
        }
    }

    /// <summary>
    /// Safe wrapper for fire-and-forget LoadDocumentsAsync to prevent unobserved exceptions
    /// </summary>
    private async void SafeLoadDocumentsAsync()
    {
        try
        {
            await LoadDocumentsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception in SafeLoadDocumentsAsync", ex);
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfDocumentViewModel.IsSelected))
        {
            CreateJobCommand.NotifyCanExecuteChanged();
            UpdateDocumentCounts();
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        CreateJobCommand.NotifyCanExecuteChanged();
        PrintLabelsCommand.NotifyCanExecuteChanged();
    }

    partial void OnDestinationBaseFolderChanged(string? value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        CreateJobCommand.NotifyCanExecuteChanged();
        SafePersistSettingsAsync();
    }

    partial void OnPreviewCacheLimitMbChanged(int value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        _previewService.ConfigureCacheLimit(value);
        SafePersistSettingsAsync();
    }

    partial void OnSourceFolderChanged(string? value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        RefreshCommand.NotifyCanExecuteChanged();
        SafePersistSettingsAsync();
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        SafePersistSettingsAsync();
        if (!IsBusy && !string.IsNullOrWhiteSpace(SourceFolder))
        {
            SafeLoadDocumentsAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintLabels))]
    private async Task PrintLabelsAsync()
    {
        try
        {
            var labelInfo = new LabelPrintInfo
            {
                FolderName = DestinationFolderName,
                DestinationPath = Path.Combine(DestinationBaseFolder ?? "", DestinationFolderName),
                Timestamp = DateTime.Now,
                DocumentCount = SelectedCount,
                BarcodeData = $"{DestinationFolderName}_{DateTime.Now:yyyyMMddHHmmss}"
            };

            _logger.LogInfo($"Opening label print dialog for folder: {DestinationFolderName}");
            var appSettings = await _settingsService.LoadAsync(CancellationToken.None);
            var printed = await _labelPrintDialogService.ShowAsync(labelInfo, appSettings);

            if (printed)
            {
                _logger.LogInfo("Label printed successfully");
            }
            else
            {
                _logger.LogInfo("Label printing cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error printing label: {ex.Message}");
        }
    }

    private bool CanPrintLabels()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(DestinationFolderName);
    }
}

