using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IMoveService
{
    Task<MoveBatchResult> MoveAsync(IEnumerable<PdfDocumentInfo> documents,
                                     string destinationBase,
                                     string destinationFolderName,
                                     IProgress<MoveProgress>? progress,
                                     CancellationToken cancellationToken);
}
