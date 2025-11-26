namespace PDFOrtnerSorter.Services.Abstractions;

public interface IFolderPicker
{
    string? PickFolder(string? initialPath, string description);
}
