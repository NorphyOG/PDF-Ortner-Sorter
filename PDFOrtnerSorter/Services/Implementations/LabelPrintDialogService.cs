using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PDFOrtnerSorter.Dialogs;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;
using PDFOrtnerSorter.ViewModels;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class LabelPrintDialogService : ILabelPrintDialogService
{
    private readonly ILabelPrintService _labelPrintService;
    private readonly ILoggerService _logger;

    public LabelPrintDialogService(ILabelPrintService labelPrintService, ILoggerService logger)
    {
        _labelPrintService = labelPrintService;
        _logger = logger;
    }

    public Task<bool> ShowAsync(LabelPrintInfo labelInfo)
    {
        return ShowAsync(labelInfo, null);
    }

    public Task<bool> ShowAsync(LabelPrintInfo labelInfo, AppSettings? appSettings)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.FromResult(false);
        }

        var tcs = new TaskCompletionSource<bool>();

        dispatcher.BeginInvoke(new Action(() =>
        {
            var owner = Application.Current?.Windows.Count > 0
                ? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow
                : Application.Current?.MainWindow;

            var viewModel = new LabelPrintDialogViewModel(_labelPrintService, _logger)
            {
                FolderName = labelInfo.FolderName,
                DestinationPath = labelInfo.DestinationPath,
                DocumentCount = labelInfo.DocumentCount
            };
            
            // Apply settings from AppSettings if available
            if (appSettings != null)
            {
                if (!string.IsNullOrWhiteSpace(appSettings.LabelPrinterName))
                {
                    viewModel.SelectedPrinter = appSettings.LabelPrinterName;
                }
                
                var formatIndex = appSettings.LabelFormat switch
                {
                    "Brother DK-11208 (38x90mm)" => 1,
                    "Dymo 99012 (36x89mm)" => 2,
                    _ => 0  // Default to Avery
                };
                viewModel.SelectedFormatIndex = formatIndex;
                
                viewModel.IncludeBarcode = appSettings.IncludeBarcodeOnLabel;
            }

            var dialog = new LabelPrintDialog(viewModel)
            {
                Owner = owner
            };

            var result = dialog.ShowDialog();
            tcs.SetResult(result == true);
        }));

        return tcs.Task;
    }
}
