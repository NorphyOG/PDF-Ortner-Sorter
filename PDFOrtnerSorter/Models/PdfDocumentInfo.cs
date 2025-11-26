namespace PDFOrtnerSorter.Models;

public sealed class PdfDocumentInfo
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public required long Length { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
}
