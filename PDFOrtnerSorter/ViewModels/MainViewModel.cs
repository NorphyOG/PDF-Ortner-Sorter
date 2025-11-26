using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IFileService _fileService;
    private readonly IPreviewService _previewService;
    private readonly IMoveService _moveService;
    private readonly ISettingsService _settingsService;
    private readonly ICatalogStore _catalogStore;
    private readonly IFolderPicker _folderPicker;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly IMoveConfirmationService _moveConfirmationService;
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly ILoggerService _logger;

    private readonly ObservableCollection<PdfDocumentViewModel> _documents = new();
    private CancellationTokenSource? _loadCts;
    private bool _suspendSettingsPropagation;

    public MainViewModel(
        IFileService fileService,
        IPreviewService previewService,
        IMoveService moveService,
        ISettingsService settingsService,
        ICatalogStore catalogStore,
        IFolderPicker folderPicker,
        IBackgroundJobQueue backgroundJobQueue,
        IMoveConfirmationService moveConfirmationService,
        ISettingsDialogService settingsDialogService,
        ILoggerService logger)
    {
        _fileService = fileService;
        _previewService = previewService;
        _moveService = moveService;
        _settingsService = settingsService;
        _catalogStore = catalogStore;
        _folderPicker = folderPicker;
        _backgroundJobQueue = backgroundJobQueue;
        _moveConfirmationService = moveConfirmationService;
        _settingsDialogService = settingsDialogService;
        _logger = logger;

        Documents = new ReadOnlyObservableCollection<PdfDocumentViewModel>(_documents);
        DestinationFolderName = $"Sortierung_{DateTime.Now:yyyyMMdd}";
    }

    public ReadOnlyObservableCollection<PdfDocumentViewModel> Documents { get; }

    public int SelectedCount => _documents.Count(d => d.IsSelected);

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
    private double _moveProgress;

    [ObservableProperty]
    private int _previewCacheLimitMb = 512;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        SourceFolder = settings.LastSourceFolder;
        DestinationBaseFolder = settings.LastDestinationFolder;
        DestinationFolderName = string.IsNullOrWhiteSpace(settings.LastDestinationFolderName)
            ? DestinationFolderName
            : settings.LastDestinationFolderName;
        IncludeSubfolders = settings.IncludeSubdirectories;
        PreviewCacheLimitMb = settings.PreviewCacheLimitMb <= 0 ? 512 : settings.PreviewCacheLimitMb;
        _previewService.ConfigureCacheLimit(PreviewCacheLimitMb);

        if (!string.IsNullOrWhiteSpace(SourceFolder))
        {
            await LoadDocumentsAsync(cancellationToken);
        }
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
        var current = new SettingsDialogResult(
            SourceFolder,
            DestinationBaseFolder,
            string.IsNullOrWhiteSpace(DestinationFolderName)
                ? $"Sortierung_{DateTime.Now:yyyyMMdd}"
                : DestinationFolderName,
            IncludeSubfolders,
            PreviewCacheLimitMb);

        var result = await _settingsDialogService.ShowAsync(current);
        if (result is null)
        {
            return;
        }

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
        await PersistSettingsAsync(CancellationToken.None);

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

    [RelayCommand(CanExecute = nameof(CanMoveSelection))]
    private async Task MoveSelectionAsync()
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
            StatusMessage = "Verschieben abgebrochen.";
            return;
        }

        if (!string.Equals(DestinationFolderName, confirmedFolderName, StringComparison.Ordinal))
        {
            DestinationFolderName = confirmedFolderName;
            await PersistSettingsAsync(CancellationToken.None);
        }

        IsBusy = true;
        StatusMessage = "Verschieben läuft...";
        MoveProgress = 0;
        _logger.LogInfo($"Move started for {selected.Count} Dateien -> {DestinationBaseFolder}\\{DestinationFolderName}");

        var progress = new Progress<MoveProgress>(value =>
        {
            MoveProgress = value.Total == 0 ? 0 : (double)value.Completed / value.Total;
            StatusMessage = value.CurrentFile is null
                ? $"{value.Completed}/{value.Total} verschoben"
                : $"{value.Completed}/{value.Total} – {value.CurrentFile}";
        });

        try
        {
            var result = await _moveService.MoveAsync(selected, DestinationBaseFolder!, DestinationFolderName, progress, CancellationToken.None);
            StatusMessage = result.Failures.Count == 0
                ? $"{result.SuccessCount} Dateien nach {result.DestinationFolder} verschoben"
                : $"{result.SuccessCount} Dateien verschoben, {result.Failures.Count} Fehler";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            _logger.LogError("MoveSelectionAsync failed", ex);
        }
        finally
        {
            MoveProgress = 0;
            IsBusy = false;
            await LoadDocumentsAsync(CancellationToken.None);
        }
    }

    private bool CanMoveSelection()
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

        MoveSelectionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedCount));
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var document in _documents)
        {
            document.IsSelected = false;
        }

        MoveSelectionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedCount));
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
                MoveSelectionCommand.NotifyCanExecuteChanged();
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

        var settings = new AppSettings
        {
            LastSourceFolder = SourceFolder,
            LastDestinationFolder = DestinationBaseFolder,
            LastDestinationFolderName = DestinationFolderName,
            IncludeSubdirectories = IncludeSubfolders,
            PreviewCacheLimitMb = PreviewCacheLimitMb
        };

        await _settingsService.SaveAsync(settings, cancellationToken);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PdfDocumentViewModel.IsSelected))
        {
            MoveSelectionCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        MoveSelectionCommand.NotifyCanExecuteChanged();
    }

    partial void OnDestinationBaseFolderChanged(string? value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        MoveSelectionCommand.NotifyCanExecuteChanged();
        _ = PersistSettingsAsync(CancellationToken.None);
    }

    partial void OnPreviewCacheLimitMbChanged(int value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        _previewService.ConfigureCacheLimit(value);
        _ = PersistSettingsAsync(CancellationToken.None);
    }

    partial void OnSourceFolderChanged(string? value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        RefreshCommand.NotifyCanExecuteChanged();
        _ = PersistSettingsAsync(CancellationToken.None);
    }

    partial void OnDestinationFolderNameChanged(string value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        _ = PersistSettingsAsync(CancellationToken.None);
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        if (_suspendSettingsPropagation)
        {
            return;
        }

        _ = PersistSettingsAsync(CancellationToken.None);
        if (!IsBusy && !string.IsNullOrWhiteSpace(SourceFolder))
        {
            _ = LoadDocumentsAsync(CancellationToken.None);
        }
    }
}
