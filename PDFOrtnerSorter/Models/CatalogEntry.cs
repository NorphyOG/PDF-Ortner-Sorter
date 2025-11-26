namespace PDFOrtnerSorter.Models;

public sealed class CatalogEntry
{
    public required string Path { get; init; }
    public required long Length { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public required DateTime IndexedAtUtc { get; init; }
}
