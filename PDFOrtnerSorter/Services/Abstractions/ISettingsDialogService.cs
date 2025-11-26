using System.Threading.Tasks;
using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ISettingsDialogService
{
    Task<SettingsDialogResult?> ShowAsync(SettingsDialogResult currentSettings);
}
