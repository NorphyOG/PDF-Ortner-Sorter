using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.ViewModels;

public sealed partial class PdfDocumentViewModel : ObservableObject
{
    private readonly IPreviewService _previewService;
    private readonly IBackgroundJobQueue _backgroundJobQueue;
    private readonly ObservableCollection<BitmapImage> _thumbnails = new();
    private readonly object _previewLock = new();
    private bool _previewRequested;

    public PdfDocumentViewModel(PdfDocumentInfo document,
                                IPreviewService previewService,
                                IBackgroundJobQueue backgroundJobQueue)
    {
        Document = document;
        _previewService = previewService;
        _backgroundJobQueue = backgroundJobQueue;
        Thumbnails = new ReadOnlyObservableCollection<BitmapImage>(_thumbnails);
    }

    public PdfDocumentInfo Document { get; }

    public string FileName => Document.FileName;
    public string DisplaySize => FormatSize(Document.Length);
    public string Location => Document.FullPath;

    public ReadOnlyObservableCollection<BitmapImage> Thumbnails { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPreviewLoaded;

    public void EnsurePreview(CancellationToken cancellationToken)
    {
        lock (_previewLock)
        {
            if (_previewRequested)
            {
                return;
            }

            _previewRequested = true;
        }

        _ = _backgroundJobQueue.QueueAsync(async token =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var preview = await _previewService.GetPreviewAsync(Document, token).ConfigureAwait(false);
                var images = preview.ImagePaths.Select(CreateImage).Where(b => b is not null).ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _thumbnails.Clear();
                    foreach (var image in images)
                    {
                        if (image is not null)
                        {
                            _thumbnails.Add(image);
                        }
                    }

                    IsPreviewLoaded = _thumbnails.Count > 0;
                });
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellations triggered by list refreshes.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview error for {Document.FileName}: {ex.Message}");
            }
        }, cancellationToken);
    }

    private static BitmapImage? CreateImage(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static string FormatSize(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }
}
