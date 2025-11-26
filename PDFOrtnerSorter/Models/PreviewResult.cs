namespace PDFOrtnerSorter.Models;

public sealed class PreviewResult
{
    public IReadOnlyList<string> ImagePaths { get; init; } = Array.Empty<string>();
    public bool FromCache { get; init; }
}
