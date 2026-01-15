using System.Threading.Tasks;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IAutostartService
{
    Task<bool> IsEnabledAsync();
    Task EnableAsync();
    Task DisableAsync();
}
