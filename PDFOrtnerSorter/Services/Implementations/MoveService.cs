using System.IO;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class MoveService : IMoveService
{
    private const int BufferSize = 16 * 1024 * 1024; // 16MB buffer
    private readonly int[] _retryDelaysMs = { 1000, 2000, 4000 }; // exponential backoff

    public async Task<MoveBatchResult> MoveAsync(
        IEnumerable<PdfDocumentInfo> documents,
        string destinationBase,
        string destinationFolderName,
        IProgress<DetailedMoveProgress>? progress,
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
        var backupDirectory = Path.Combine(Path.GetTempPath(), $"PDFOrtnerSorter_Backup_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(finalDestination);

        var materialized = documents.ToList();
        var failures = new List<MoveFailure>();
        var successCount = 0;
        var totalFiles = materialized.Count;
        var totalBytes = (long)materialized.Sum(d => d.Length);
        var bytesTransferred = 0L;
        var copiedFiles = new List<(string source, string backup)>();
        var startTime = DateTime.UtcNow;

        try
        {
            for (var i = 0; i < materialized.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = materialized[i];
                var targetPath = EnsureUniquePath(Path.Combine(finalDestination, document.FileName));

                try
                {
                    var fileProgress = new Progress<long>(bytesThisFile =>
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        var speedMbps = elapsed.TotalSeconds > 0 ? (bytesTransferred + bytesThisFile) / elapsed.TotalSeconds / 1024 / 1024 : 0;
                        var remainingBytes = totalBytes - bytesTransferred - bytesThisFile;
                        TimeSpan? estimatedRemaining = null;
                        if (speedMbps > 0)
                        {
                            var remainingSeconds = remainingBytes / (speedMbps * 1024 * 1024);
                            estimatedRemaining = TimeSpan.FromSeconds(remainingSeconds);
                        }

                        progress?.Report(new DetailedMoveProgress
                        {
                            BytesTransferred = bytesTransferred + bytesThisFile,
                            TotalBytes = totalBytes,
                            SpeedMBps = speedMbps,
                            EstimatedTimeRemaining = estimatedRemaining,
                            CurrentFileName = document.FileName,
                            CurrentFileBytes = bytesThisFile,
                            CurrentFileTotalBytes = document.Length,
                            CompletedFiles = i,
                            TotalFiles = totalFiles,
                            IsSlowTransfer = speedMbps > 0 && speedMbps < 10
                        });
                    });

                    await CopyFileWithProgressAsync(document.FullPath, targetPath, fileProgress, cancellationToken);
                    
                    // Create backup of original file before deletion
                    var backupPath = Path.Combine(backupDirectory, Path.GetFileName(document.FullPath));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                    File.Copy(document.FullPath, backupPath, overwrite: true);
                    copiedFiles.Add((document.FullPath, backupPath));

                    bytesTransferred += document.Length;
                    successCount++;
                }
                catch (Exception ex)
                {
                    failures.Add(new MoveFailure
                    {
                        SourcePath = document.FullPath,
                        Reason = ex.Message
                    });
                }
            }

            // Delete original files after successful batch
            foreach (var (source, backup) in copiedFiles)
            {
                try
                {
                    File.Delete(source);
                }
                catch
                {
                    // If deletion fails, keep the backup for recovery
                }
            }

            // Clean up backup directory if all successful
            if (failures.Count == 0)
            {
                try
                {
                    Directory.Delete(backupDirectory, recursive: true);
                }
                catch
                {
                    // If cleanup fails, leave backup for manual recovery
                }
            }
        }
        catch (Exception)
        {
            // On critical error, ensure backups are available
            if (!Directory.Exists(backupDirectory) || Directory.EnumerateFiles(backupDirectory).Count() == 0)
            {
                throw;
            }
        }

        return new MoveBatchResult
        {
            DestinationFolder = finalDestination,
            RequestedCount = totalFiles,
            SuccessCount = successCount,
            Failures = failures,
            BytesTransferred = bytesTransferred,
            BackupDirectory = failures.Count > 0 ? backupDirectory : null
        };
    }

    private async Task CopyFileWithProgressAsync(
        string sourcePath,
        string destinationPath,
        IProgress<long>? progress,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        while (true)
        {
            try
            {
                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
                using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
                
                var buffer = new byte[BufferSize];
                int bytesRead;
                long totalBytesRead = 0;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }

                return; // Success
            }
            catch (Exception) when (retryCount < _retryDelaysMs.Length)
            {
                // Clean up partial file
                try
                {
                    File.Delete(destinationPath);
                }
                catch { }

                // Wait with exponential backoff
                await Task.Delay(_retryDelaysMs[retryCount], cancellationToken);
                retryCount++;
            }
        }
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
