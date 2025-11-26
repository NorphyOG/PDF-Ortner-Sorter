using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PDFOrtnerSorter.Dialogs;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly IFolderPicker _folderPicker;

    public SettingsDialogService(IFolderPicker folderPicker)
    {
        _folderPicker = folderPicker;
    }

    public Task<SettingsDialogResult?> ShowAsync(SettingsDialogResult currentSettings)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.FromResult<SettingsDialogResult?>(currentSettings);
        }

        var tcs = new TaskCompletionSource<SettingsDialogResult?>();

        dispatcher.BeginInvoke(new Action(() =>
        {
            var owner = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow
                : Application.Current?.MainWindow;

            var viewModel = new SettingsDialogViewModel(_folderPicker);
            viewModel.ApplyFrom(currentSettings);

            var dialog = new SettingsWindow(viewModel)
            {
                Owner = owner
            };

            var result = dialog.ShowDialog();
            tcs.SetResult(result == true ? viewModel.ToResult() : null);
        }));

        return tcs.Task;
    }
}
