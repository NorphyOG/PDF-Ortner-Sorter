namespace PDFOrtnerSorter.Models;

public sealed class MoveBatchResult
{
    public required string DestinationFolder { get; init; }
    public required int RequestedCount { get; init; }
    public required int SuccessCount { get; init; }
    public IReadOnlyCollection<MoveFailure> Failures { get; init; } = Array.Empty<MoveFailure>();
}

public sealed class MoveFailure
{
    public required string SourcePath { get; init; }
    public required string Reason { get; init; }
}

public readonly record struct MoveProgress(int Completed, int Total, string? CurrentFile);
