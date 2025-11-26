using System.IO;
using Ookii.Dialogs.Wpf;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class FolderPickerService : IFolderPicker
{
    public string? PickFolder(string? initialPath, string description)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.SelectedPath = initialPath;
        }

        var result = dialog.ShowDialog();
        return result is true ? dialog.SelectedPath : null;
    }
}
