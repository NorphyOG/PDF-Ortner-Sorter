using System.IO;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class FileService : IFileService
{
    public async IAsyncEnumerable<PdfDocumentInfo> EnumerateAsync(
        string folder,
        bool includeSubdirectories,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            yield break;
        }

        var option = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.EnumerateFiles(folder, "*.pdf", option))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            yield return new PdfDocumentInfo
            {
                FileName = info.Name,
                FullPath = info.FullName,
                Length = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc
            };

            // Yield to UI thread to keep list responsive on very large collections
            await Task.Yield();
        }
    }
}
