namespace PDFOrtnerSorter.Models;

public sealed record SettingsDialogResult(
    string? SourceFolder,
    string? DestinationBaseFolder,
    string DestinationFolderName,
    bool IncludeSubfolders,
    int PreviewCacheLimitMb,
    AppSettings Settings);
