using System.Threading.Tasks;
using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ILabelPrintDialogService
{
    Task<bool> ShowAsync(LabelPrintInfo labelInfo);
    Task<bool> ShowAsync(LabelPrintInfo labelInfo, AppSettings? appSettings);
}
