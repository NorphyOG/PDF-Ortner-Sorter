using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PDFOrtnerSorter.Models;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface ILabelPrintService
{
    /// <summary>
    /// Gets a list of available printers on the system
    /// </summary>
    IReadOnlyList<string> GetAvailablePrinters();

    /// <summary>
    /// Prints labels with the specified information
    /// </summary>
    Task<bool> PrintLabelAsync(LabelPrintRequest request, string printerName, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a preview image of the label
    /// </summary>
    Task<byte[]?> GenerateLabelPreviewAsync(LabelPrintRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Exports label as PDF
    /// </summary>
    Task<bool> ExportLabelAsPdfAsync(LabelPrintRequest request, string outputPath, CancellationToken cancellationToken);
}
