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
    private readonly ILabelPrintService _labelPrintService;

    public SettingsDialogService(IFolderPicker folderPicker, ILabelPrintService labelPrintService)
    {
        _folderPicker = folderPicker;
        _labelPrintService = labelPrintService;
    }

    public Task<SettingsDialogResult?> ShowAsync(SettingsDialogResult currentSettings)
    {
        return ShowAsync(currentSettings, null);
    }

    public Task<SettingsDialogResult?> ShowAsync(SettingsDialogResult currentSettings, AppSettings? appSettings)
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

            var viewModel = new SettingsDialogViewModel(_folderPicker, _labelPrintService);
            viewModel.ApplyFrom(currentSettings);
            
            if (appSettings != null)
            {
                viewModel.ApplyFromAppSettings(appSettings);
            }

            var dialog = new SettingsWindow(viewModel)
            {
                Owner = owner
            };

            var dialogResult = dialog.ShowDialog();
            if (dialogResult == true)
            {
                // Save the complete AppSettings from the ViewModel
                var updatedAppSettings = viewModel.ToAppSettings();
                SaveSettingsInBackground(updatedAppSettings);
                
                var result = viewModel.ToResult();
                // Include the full AppSettings in the result
                var resultWithSettings = result with { Settings = updatedAppSettings };
                tcs.SetResult(resultWithSettings);
            }
            else
            {
                tcs.SetResult(null);
            }
        }));

        return tcs.Task;
    }

    /// <summary>
    /// Safe wrapper for background settings save to prevent unobserved exceptions
    /// </summary>
    private async void SaveSettingsInBackground(AppSettings settings)
    {
        try
        {
            var settingsService = new SettingsService();
            await settingsService.SaveAsync(settings, System.Threading.CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Background save error: {ex.Message}");
        }
    }
}
