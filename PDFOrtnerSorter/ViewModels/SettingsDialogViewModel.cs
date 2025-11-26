using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class SettingsDialogViewModel : ObservableObject
{
    private readonly IFolderPicker _folderPicker;

    public SettingsDialogViewModel(IFolderPicker folderPicker)
    {
        _folderPicker = folderPicker;
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

    public SettingsDialogResult ToResult()
    {
        var cacheLimit = PreviewCacheLimitMb <= 0 ? 512 : PreviewCacheLimitMb;
        var folderName = string.IsNullOrWhiteSpace(DestinationFolderName)
            ? $"Sortierung_{System.DateTime.Now:yyyyMMdd}"
            : DestinationFolderName.Trim();

        return new SettingsDialogResult(SourceFolder, DestinationBaseFolder, folderName, IncludeSubfolders, cacheLimit);
    }

    partial void OnSourceFolderChanged(string? value) => OnPropertyChanged(nameof(CanSave));
    partial void OnDestinationBaseFolderChanged(string? value) => OnPropertyChanged(nameof(CanSave));
}
