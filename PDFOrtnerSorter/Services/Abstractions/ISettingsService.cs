using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
