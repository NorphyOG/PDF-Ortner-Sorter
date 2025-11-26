using System.Threading.Tasks;

namespace PDFOrtnerSorter.Services.Abstractions;

public interface IMoveConfirmationService
{
    /// <summary>
    /// Shows a confirmation dialog that allows the user to review and optionally edit the destination folder name.
    /// Returns the confirmed folder name or <c>null</c> when the user cancels the operation.
    /// </summary>
    /// <param name="currentFolderName">Pre-filled folder name.</param>
    /// <returns>The confirmed folder name, or <c>null</c> if the dialog was canceled.</returns>
    Task<string?> ConfirmDestinationFolderAsync(string currentFolderName);
}
