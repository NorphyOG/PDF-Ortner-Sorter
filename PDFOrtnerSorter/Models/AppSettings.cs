using System.Text.Json.Serialization;

namespace PDFOrtnerSorter.Models;

public sealed class AppSettings
{
    public string? LastSourceFolder { get; set; }
    public string? LastDestinationFolder { get; set; }
    public string? LastDestinationFolderName { get; set; }
    public bool IncludeSubdirectories { get; set; } = true;
    public int PreviewCacheLimitMb { get; set; } = 512;

    [JsonIgnore]
    public static AppSettings Default => new();
}
