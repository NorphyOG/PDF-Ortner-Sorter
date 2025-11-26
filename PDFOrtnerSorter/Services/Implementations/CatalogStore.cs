using System.IO;
using System.Text.Json;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class CatalogStore : ICatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly string _catalogPath;

    public CatalogStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFOrtnerSorter");
        Directory.CreateDirectory(root);
        _catalogPath = Path.Combine(root, "catalog.json");
    }

    public async Task SaveSnapshotAsync(IEnumerable<CatalogEntry> entries, CancellationToken cancellationToken)
    {
        var materialized = entries.ToArray();
        await using var stream = File.Create(_catalogPath);
        await JsonSerializer.SerializeAsync(stream, materialized, SerializerOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogEntry>> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_catalogPath))
        {
            return Array.Empty<CatalogEntry>();
        }

        await using var stream = File.OpenRead(_catalogPath);
        var data = await JsonSerializer.DeserializeAsync<IReadOnlyList<CatalogEntry>>(stream, SerializerOptions, cancellationToken);
        return data ?? Array.Empty<CatalogEntry>();
    }
}
