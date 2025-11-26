using System.IO;
using System.Text.Json;
using PDFOrtnerSorter.Models;
using PDFOrtnerSorter.Services.Abstractions;

namespace PDFOrtnerSorter.Services.Implementations;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var settingsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDFOrtnerSorter");
        Directory.CreateDirectory(settingsRoot);
        _settingsPath = Path.Combine(settingsRoot, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsPath))
        {
            return AppSettings.Default;
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
        return settings ?? AppSettings.Default;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
