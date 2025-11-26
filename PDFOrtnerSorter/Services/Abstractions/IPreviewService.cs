using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IPreviewService
{
    Task<PreviewResult> GetPreviewAsync(PdfDocumentInfo document, CancellationToken cancellationToken);
    void ConfigureCacheLimit(int megabytes);
}
