using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IFileService
{
    IAsyncEnumerable<PdfDocumentInfo> EnumerateAsync(string folder, bool includeSubdirectories, CancellationToken cancellationToken);
}
