using System.IO;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class MoveService : IMoveService
{
    public async Task<MoveBatchResult> MoveAsync(
        IEnumerable<PdfDocumentInfo> documents,
        string destinationBase,
        string destinationFolderName,
        IProgress<MoveProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destinationBase))
        {
            throw new ArgumentException("Destination base folder is required", nameof(destinationBase));
        }

        Directory.CreateDirectory(destinationBase);

        var targetFolderName = string.IsNullOrWhiteSpace(destinationFolderName)
            ? $"Batch_{DateTime.Now:yyyyMMdd_HHmmss}"
            : destinationFolderName.Trim();

        var finalDestination = Path.Combine(destinationBase, targetFolderName);
        Directory.CreateDirectory(finalDestination);

        var materialized = documents.ToList();
        var failures = new List<MoveFailure>();
        var total = materialized.Count;
        var completed = 0;

        foreach (var document in materialized)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = EnsureUniquePath(Path.Combine(finalDestination, document.FileName));
            try
            {
                await Task.Run(() => File.Move(document.FullPath, targetPath, overwrite: false), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failures.Add(new MoveFailure
                {
                    SourcePath = document.FullPath,
                    Reason = ex.Message
                });
            }
            finally
            {
                completed++;
                progress?.Report(new MoveProgress(completed, total, document.FileName));
            }
        }

        return new MoveBatchResult
        {
            DestinationFolder = finalDestination,
            RequestedCount = total,
            SuccessCount = total - failures.Count,
            Failures = failures
        };
    }

    private static string EnsureUniquePath(string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var directory = Path.GetDirectoryName(destinationPath)!;
        var name = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);
        var counter = 1;

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}_{counter}{extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }
}
