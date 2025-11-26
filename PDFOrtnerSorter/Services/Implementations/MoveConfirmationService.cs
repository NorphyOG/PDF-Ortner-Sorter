using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PDFOrtnerSorter.Dialogs;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class MoveConfirmationService : IMoveConfirmationService
{
    public Task<string?> ConfirmDestinationFolderAsync(string currentFolderName)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.FromResult<string?>(currentFolderName);
        }

        var tcs = new TaskCompletionSource<string?>();

        dispatcher.BeginInvoke(new Action(() =>
        {
            var owner = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow
                : Application.Current?.MainWindow;

            var dialog = new MoveConfirmationWindow(currentFolderName)
            {
                Owner = owner
            };

            var result = dialog.ShowDialog();
            tcs.SetResult(result == true ? dialog.FolderName : null);
        }));

        return tcs.Task;
    }
}
