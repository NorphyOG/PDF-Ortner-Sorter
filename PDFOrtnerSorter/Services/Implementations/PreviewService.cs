using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using PdfiumViewer;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class PreviewService : IPreviewService, IDisposable
{
    private const long DefaultCacheLimitBytes = 512L * 1024 * 1024; // 512 MB
    private readonly string _cacheRoot;
    private readonly SemaphoreSlim _renderSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, object> _locks = new();
    private long _cacheLimitBytes = DefaultCacheLimitBytes;

    public PreviewService()
    {
        _cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFOrtnerSorter", "PreviewCache");
        Directory.CreateDirectory(_cacheRoot);
    }

    public void ConfigureCacheLimit(int megabytes)
    {
        if (megabytes <= 0)
        {
            return;
        }

        _cacheLimitBytes = megabytes * 1024L * 1024L;
    }

    public async Task<PreviewResult> GetPreviewAsync(PdfDocumentInfo document, CancellationToken cancellationToken)
    {
        if (!File.Exists(document.FullPath))
        {
            return new PreviewResult();
        }

        var cacheKey = BuildKey(document);
        var previewFolder = Path.Combine(_cacheRoot, cacheKey);
        Directory.CreateDirectory(previewFolder);

        var lockObj = _locks.GetOrAdd(cacheKey, _ => new object());
        var cachedPaths = new List<string>();
        var fromCache = true;

        for (var page = 0; page < 3; page++)
        {
            var pagePath = Path.Combine(previewFolder, $"page_{page}.png");
            cachedPaths.Add(pagePath);
        }

        if (cachedPaths.All(File.Exists))
        {
            return new PreviewResult { ImagePaths = cachedPaths, FromCache = true };
        }

        await _renderSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (lockObj)
            {
                if (cachedPaths.All(File.Exists))
                {
                    return new PreviewResult { ImagePaths = cachedPaths, FromCache = true };
                }

                using var pdf = PdfDocument.Load(document.FullPath);
                var pageCount = Math.Min(3, pdf.PageCount);
                for (var page = 0; page < pageCount; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var targetPath = cachedPaths[page];
                    RenderPage(pdf, page, targetPath);
                }

                fromCache = false;
            }
        }
        finally
        {
            _renderSemaphore.Release();
            _locks.TryRemove(cacheKey, out _);
        }

        await Task.Run(PruneCacheIfNeeded, cancellationToken).ConfigureAwait(false);
        return new PreviewResult { ImagePaths = cachedPaths, FromCache = fromCache };
    }

    private void RenderPage(PdfDocument document, int pageIndex, string targetPath)
    {
        const int targetHeight = 220;
        var size = document.PageSizes[pageIndex];
        var scale = targetHeight / size.Height;
        var width = Math.Max(160, (int)(size.Width * scale));
        var height = Math.Max(160, (int)(size.Height * scale));

        using var bitmap = document.Render(pageIndex, width, height, 96, 96, PdfRenderFlags.Annotations);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        bitmap.Save(targetPath, ImageFormat.Png);
    }

    private string BuildKey(PdfDocumentInfo document)
    {
        using var sha = SHA256.Create();
        var buffer = Encoding.UTF8.GetBytes($"{document.FullPath}|{document.Length}|{document.LastWriteTimeUtc.Ticks}");
        var hash = Convert.ToHexString(sha.ComputeHash(buffer));
        return hash;
    }

    private void PruneCacheIfNeeded()
    {
        try
        {
            var directory = new DirectoryInfo(_cacheRoot);
            if (!directory.Exists)
            {
                return;
            }

            var files = directory.GetDirectories();
            long totalSize = files.Sum(folder => folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length));
            if (totalSize <= _cacheLimitBytes)
            {
                return;
            }

            foreach (var folder in files.OrderBy(dir => dir.LastAccessTimeUtc))
            {
                try
                {
                    var folderSize = folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                    folder.Delete(recursive: true);
                    totalSize -= folderSize;
                }
                catch
                {
                    // Ignore deletion failures; cache cleanup best effort.
                }

                if (totalSize <= _cacheLimitBytes)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cache pruning failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _renderSemaphore.Dispose();
    }
}
