namespace PDFOrtnerSorter.Models;

public sealed class MoveBatchResult
{
    public required string DestinationFolder { get; init; }
    public required int RequestedCount { get; init; }
    public required int SuccessCount { get; init; }
    public IReadOnlyCollection<MoveFailure> Failures { get; init; } = Array.Empty<MoveFailure>();
    public long BytesTransferred { get; init; }
    public string? BackupDirectory { get; init; }
}

public sealed class MoveFailure
{
    public required string SourcePath { get; init; }
    public required string Reason { get; init; }
}

public readonly record struct MoveProgress(int Completed, int Total, string? CurrentFile);

public readonly record struct DetailedMoveProgress
{
    public long BytesTransferred { get; init; }
    public long TotalBytes { get; init; }
    public double SpeedMBps { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public string? CurrentFileName { get; init; }
    public long CurrentFileBytes { get; init; }
    public long CurrentFileTotalBytes { get; init; }
    public int CompletedFiles { get; init; }
    public int TotalFiles { get; init; }
    public bool IsSlowTransfer { get; init; }
}
