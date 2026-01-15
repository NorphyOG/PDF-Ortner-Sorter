using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class LabelPrintDialogViewModel : ObservableObject
{
    private readonly ILabelPrintService _labelPrintService;
    private readonly ILoggerService _logger;

    public LabelPrintDialogViewModel(ILabelPrintService labelPrintService, ILoggerService logger)
    {
        _labelPrintService = labelPrintService;
        _logger = logger;
        
        AvailablePrinters = new ObservableCollection<string>();
        RefreshPrinters();

        AvailableFormats = new ObservableCollection<string>
        {
            "Avery Zweckform 3474 (70x36mm)",
            "Brother DK-11208 (38x90mm)",
            "Dymo 99012 (36x89mm)"
        };
        SelectedFormatIndex = 0;
    }

    [RelayCommand]
    private void RefreshPrinters()
    {
        try
        {
            var printers = _labelPrintService.GetAvailablePrinters();
            _logger.LogInfo($"Found {printers.Count} printer(s)");
            
            AvailablePrinters.Clear();
            foreach (var printer in printers)
            {
                _logger.LogInfo($"  - {printer}");
                AvailablePrinters.Add(printer);
            }

            if (!string.IsNullOrWhiteSpace(SelectedPrinter) && !AvailablePrinters.Contains(SelectedPrinter))
            {
                SelectedPrinter = null;
            }

            if (string.IsNullOrWhiteSpace(SelectedPrinter))
            {
                SelectedPrinter = AvailablePrinters.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to refresh printers", ex);
        }
    }

    public ObservableCollection<string> AvailablePrinters { get; }
    public ObservableCollection<string> AvailableFormats { get; }

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private int _selectedFormatIndex;

    [ObservableProperty]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private int _documentCount;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private bool _includeBarcode;

    [ObservableProperty]
    private BitmapImage? _previewImage;

    [ObservableProperty]
    private bool _isGeneratingPreview;

    public bool CanPrint => !string.IsNullOrWhiteSpace(SelectedPrinter) && !string.IsNullOrWhiteSpace(FolderName);

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task PrintAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPrinter))
            return;

        var request = CreatePrintRequest();
        var success = await _labelPrintService.PrintLabelAsync(request, SelectedPrinter, CancellationToken.None);
        
        if (success)
        {
            _logger.LogInfo($"Successfully printed {Copies} label(s) for '{FolderName}'");
        }
    }

    [RelayCommand]
    private async Task GeneratePreviewAsync()
    {
        IsGeneratingPreview = true;
        try
        {
            var request = CreatePrintRequest();
            var imageBytes = await _labelPrintService.GenerateLabelPreviewAsync(request, CancellationToken.None);
            
            if (imageBytes != null)
            {
                PreviewImage = BytesToBitmapImage(imageBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to generate preview", ex);
        }
        finally
        {
            IsGeneratingPreview = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsPdfAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png",
            FileName = $"Etikett_{FolderName}_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() == true)
        {
            var request = CreatePrintRequest();
            await _labelPrintService.ExportLabelAsPdfAsync(request, dialog.FileName, CancellationToken.None);
        }
    }

    private LabelPrintRequest CreatePrintRequest()
    {
        var format = SelectedFormatIndex switch
        {
            1 => LabelFormat.BrotherDK11208,
            2 => LabelFormat.Dymo99012,
            _ => LabelFormat.Avery3474
        };

        return new LabelPrintRequest
        {
            LabelInfo = new LabelPrintInfo
            {
                FolderName = FolderName,
                DestinationPath = DestinationPath,
                Timestamp = DateTime.Now,
                DocumentCount = DocumentCount,
                BarcodeData = IncludeBarcode ? GenerateBarcodeJson() : null
            },
            Format = format,
            Copies = Copies,
            IncludeBarcode = IncludeBarcode
        };
    }

    private static BitmapImage BytesToBitmapImage(byte[] bytes)
    {
        var image = new BitmapImage();
        using (var stream = new System.IO.MemoryStream(bytes))
        {
            stream.Position = 0;
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
        }
        image.Freeze();
        return image;
    }

    partial void OnSelectedPrinterChanged(string? value) => OnPropertyChanged(nameof(CanPrint));

    private async void SafeGeneratePreview()
    {
        try
        {
            await GeneratePreviewAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Unhandled exception in SafeGeneratePreview", ex);
        }
    }

    partial void OnDocumentCountChanged(int value)
    {
        // Validate document count - minimum 1
        if (value < 1)
        {
            DocumentCount = 1;
        }
        // Maximum 999
        if (value > 999)
        {
            DocumentCount = 999;
        }
        SafeGeneratePreview();
    }

    partial void OnCopiesChanged(int value)
    {
        // Validate copies - minimum 1
        if (value < 1)
        {
            Copies = 1;
        }
        // Maximum 99
        if (value > 99)
        {
            Copies = 99;
        }
    }

    partial void OnFolderNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanPrint));
        SafeGeneratePreview();
    }

    partial void OnDestinationPathChanged(string value)
    {
        SafeGeneratePreview();
    }

    partial void OnSelectedFormatIndexChanged(int value)
    {
        SafeGeneratePreview();
    }

    partial void OnIncludeBarcodeChanged(bool value)
    {
        SafeGeneratePreview();
    }

    private string GenerateBarcodeJson()
    {
        var data = new
        {
            ordnerName = FolderName,
            zielordner = DestinationPath,
            zeitstempel = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            dokumentAnzahl = DocumentCount,
            erstelltAm = DateTime.Now.ToString("yyyyMMdd")
        };

        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null
        });
    }
}
