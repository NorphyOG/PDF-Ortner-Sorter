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

        try
        {
            // Use FileShare.Read to allow multiple readers but prevent write conflicts
            await using var stream = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            return settings ?? AppSettings.Default;
        }
        catch (IOException)
        {
            // If file is locked, wait a bit and return default settings
            await Task.Delay(100, cancellationToken);
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        
        // Write to temp file first, then replace atomically to avoid corruption
        var tempPath = _settingsPath + ".tmp";
        
        const int maxRetries = 3;
        var retryDelay = 100;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Use FileShare.None for exclusive write access
                await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
                }
                
                // Replace the original file atomically
                File.Move(tempPath, _settingsPath, true);
                return; // Success!
            }
            catch (IOException)
            {
                // Clean up temp file if it exists
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
                
                if (attempt == maxRetries)
                {
                    // Last attempt failed, log error but don't rethrow
                    // This prevents Task exceptions from crashing the UI
                    return;
                }
                
                // Wait before retry with exponential backoff
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
        }
    }
}
