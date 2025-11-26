using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ICatalogStore
{
    Task SaveSnapshotAsync(IEnumerable<CatalogEntry> entries, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogEntry>> LoadSnapshotAsync(CancellationToken cancellationToken);
}
