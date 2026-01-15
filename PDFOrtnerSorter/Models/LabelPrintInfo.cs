namespace PDFOrtnerSorter.Models;

public sealed class LabelPrintInfo
{
    public required string FolderName { get; init; }
    public required string DestinationPath { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int DocumentCount { get; init; }
    public string? BarcodeData { get; init; }
}

public enum LabelFormat
{
    Avery3474,      // 70x36mm
    BrotherDK11208, // 38x90mm
    Dymo99012       // 36x89mm
}

public sealed class LabelPrintRequest
{
    public required LabelPrintInfo LabelInfo { get; init; }
    public required LabelFormat Format { get; init; }
    public int Copies { get; init; } = 1;
    public bool IncludeBarcode { get; init; }
}
